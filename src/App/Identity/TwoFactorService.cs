using App;
using App.Identity.Ports;
using Domain;
using Domain.Identity;

namespace App.Identity;

public sealed class TwoFactorService(
    IUserRepository users,
    IUnitOfWork unitOfWork,
    ITotpService totpService,
    ITotpSecretProtector totpSecretProtector,
    IQrCodeGenerator qrGenerator,
    IRecoveryCodeGenerator recoveryCodeGenerator,
    IPasswordHasher passwordHasher,
    IClock clock
) : ITwoFactorService
{
    private const string Issuer = "recipes";

    public async Task<TwoFactorResult> SetupAsync(UserId userId)
    {
        var user = await users.GetByIdAsync(userId);
        if (user is null)
        {
            return new TwoFactorResult.UserNotFound();
        }

        if (user.HasTwoFactorEnabled)
        {
            return new TwoFactorResult.AlreadyEnabled();
        }

        var secret = totpService.GenerateSecret();
        var base32 = totpService.EncodeBase32(secret);
        var uri = totpService.BuildOtpAuthUri(secret, user.Email.Value, Issuer);
        var qr = qrGenerator.GeneratePng(uri);
        var protectedSecret = totpSecretProtector.Protect(secret);

        user.StartTwoFactorSetup(EncryptedTotpSecret.From(protectedSecret), clock);
        await unitOfWork.SaveChangesAsync();

        return new TwoFactorResult.SetupPending(base32, uri, Convert.ToBase64String(qr));
    }

    public async Task<TwoFactorResult> ConfirmAsync(UserId userId, string code)
    {
        var user = await users.GetByIdAsync(userId);
        if (user is null)
        {
            return new TwoFactorResult.UserNotFound();
        }

        if (user.HasTwoFactorEnabled)
        {
            return new TwoFactorResult.AlreadyEnabled();
        }

        if (user.Totp is null)
        {
            return new TwoFactorResult.NotEnabled();
        }

        var verification = VerifyTotp(user, code);
        if (verification is not TotpVerificationResult.Match match)
        {
            return new TwoFactorResult.InvalidCode();
        }

        var recoveryCodes = recoveryCodeGenerator.Generate();
        var hashes = HashRecoveryCodes(recoveryCodes);
        if (!user.ConfirmTwoFactor(match.Step, hashes, clock))
        {
            return new TwoFactorResult.NotEnabled();
        }

        try
        {
            await unitOfWork.SaveChangesAsync();
        }
        catch (ConcurrencyConflictException)
        {
            return new TwoFactorResult.InvalidCode();
        }

        return new TwoFactorResult.Enabled(recoveryCodes);
    }

    public async Task<TwoFactorResult> DisableAsync(UserId userId, string code)
    {
        var user = await users.GetByIdAsync(userId);
        if (user is null)
        {
            return new TwoFactorResult.UserNotFound();
        }

        if (!user.HasTwoFactorEnabled)
        {
            return new TwoFactorResult.NotEnabled();
        }

        if (!VerifyAndAdvanceTotp(user, code))
        {
            return new TwoFactorResult.InvalidCode();
        }

        await using var uow = await unitOfWork.BeginAsync();
        user.DisableTwoFactor(clock);

        try
        {
            await unitOfWork.SaveChangesAsync();
        }
        catch (ConcurrencyConflictException)
        {
            return new TwoFactorResult.InvalidCode();
        }

        await uow.CommitAsync();
        return new TwoFactorResult.Disabled();
    }

    public async Task<TwoFactorResult> RegenerateRecoveryCodesAsync(UserId userId, string code)
    {
        var user = await users.GetByIdAsync(userId);
        if (user is null)
        {
            return new TwoFactorResult.UserNotFound();
        }

        if (!user.HasTwoFactorEnabled)
        {
            return new TwoFactorResult.NotEnabled();
        }

        if (!VerifyAndAdvanceTotp(user, code))
        {
            return new TwoFactorResult.InvalidCode();
        }

        var recoveryCodes = recoveryCodeGenerator.Generate();
        var hashes = HashRecoveryCodes(recoveryCodes);
        user.RegenerateRecoveryCodes(hashes, clock);

        try
        {
            await unitOfWork.SaveChangesAsync();
        }
        catch (ConcurrencyConflictException)
        {
            return new TwoFactorResult.InvalidCode();
        }

        return new TwoFactorResult.CodesRegenerated(recoveryCodes);
    }

    private bool VerifyAndAdvanceTotp(User user, string code)
    {
        var verification = VerifyTotp(user, code);
        return verification is TotpVerificationResult.Match match
            && user.VerifyAndAdvanceTotp(match.Step, clock);
    }

    private TotpVerificationResult VerifyTotp(User user, string code)
    {
        if (user.Totp is null)
        {
            return new TotpVerificationResult.NoMatch();
        }

        var secret = totpSecretProtector.Unprotect(user.Totp.EncryptedSecret.Value);
        return totpService.Verify(secret, code.Trim());
    }

    private IReadOnlyList<RecoveryCodeHash> HashRecoveryCodes(IReadOnlyList<string> recoveryCodes) =>
        recoveryCodes.Select(code => RecoveryCodeHash.From(passwordHasher.Hash(code))).ToArray();
}
