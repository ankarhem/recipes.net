using Domain;
using Domain.Identity;

namespace App.Identity;

public sealed class AuthService(
    IUserRepository users,
    IUserSessionRepository userSessions,
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    IAccessTokenService accessTokenService,
    ISecureTokenGenerator secureTokenGenerator,
    ITotpService totpService,
    ITotpSecretProtector totpSecretProtector,
    IEmailWorkflowStarter emailWorkflowStarter,
    IClock clock
) : IAuthService
{
    private const int RefreshTokenDays = 7;
    private const int EmailVerificationHours = 24;
    private const int PasswordResetMinutes = 60;
    private const int TwoFactorChallengeMinutes = 5;

    public async Task<AuthResult> RegisterAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default
    )
    {
        var normalizedEmail = Email.Normalize(email);
        var existing = await users.GetByEmailAsync(normalizedEmail, cancellationToken);
        if (existing is not null)
        {
            return new AuthResult.EmailAlreadyRegistered();
        }

        var passwordHash = PasswordHash.From(passwordHasher.Hash(password));
        var user = User.Register(normalizedEmail, passwordHash, clock);

        var (plainToken, hashedToken) = secureTokenGenerator.Generate();
        var expiresAt = clock.UtcNow.AddHours(EmailVerificationHours);
        user.IssueEmailVerificationToken(TokenHash.From(hashedToken), expiresAt, clock);

        await users.AddAsync(user, cancellationToken);
        await users.SaveChangesAsync(cancellationToken);

        await emailWorkflowStarter.StartVerificationWorkflowAsync(
            user.Id.Value,
            user.Email.Value,
            plainToken,
            cancellationToken
        );

        return new AuthResult.RegistrationPending(user.Id.Value, user.Email.Value);
    }

    public async Task<AuthResult> LoginAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default
    )
    {
        var normalizedEmail = Email.Normalize(email);
        var user = await users.GetByEmailAsync(normalizedEmail, cancellationToken);

        if (user is null)
        {
            _ = passwordHasher.Verify(password, passwordHasher.DummyHash);
            return new AuthResult.InvalidCredentials();
        }

        if (!passwordHasher.Verify(password, user.PasswordHash.Value))
        {
            return new AuthResult.InvalidCredentials();
        }

        if (!user.EmailVerified)
        {
            return new AuthResult.EmailNotVerified();
        }

        if (user.HasTwoFactorEnabled)
        {
            var (plainToken, hashedToken) = secureTokenGenerator.Generate();
            var expiresAt = clock.UtcNow.AddMinutes(TwoFactorChallengeMinutes);
            user.IssueTwoFactorChallenge(TokenHash.From(hashedToken), expiresAt, clock);
            await users.SaveChangesAsync(cancellationToken);
            return new AuthResult.TwoFactorRequired(user.Id.Value, plainToken, ["totp"]);
        }

        var accessToken = accessTokenService.Generate(user.Id.Value, user.Email.Value);
        var refreshToken = await IssueSessionAsync(user.Id, cancellationToken);

        return new AuthResult.Success(user.Id.Value, user.Email.Value, accessToken, refreshToken);
    }

    public async Task<AuthResult> VerifyTotpAsync(
        string challengeToken,
        string code,
        CancellationToken cancellationToken = default
    )
    {
        var challengeHash = TokenHash.From(TokenHasher.Hash(challengeToken));
        var user = await users.GetByTwoFactorChallengeHashAsync(challengeHash, cancellationToken);
        if (user is null)
        {
            return new AuthResult.InvalidChallengeToken();
        }

        var challenge = user.TwoFactorChallenges.SingleOrDefault(c => c.TokenHash == challengeHash);
        if (challenge is null || challenge.IsConsumed)
        {
            return new AuthResult.InvalidChallengeToken();
        }

        if (challenge.IsExpired(clock.UtcNow))
        {
            return new AuthResult.ChallengeTokenExpired();
        }

        var normalizedCode = code.Trim();
        var recoveryCodeUsed = false;
        if (IsLikelyTotpCode(normalizedCode))
        {
            if (!VerifyTotpCode(user, normalizedCode))
            {
                return new AuthResult.InvalidTwoFactorCode();
            }
        }
        else
        {
            if (!ConsumeRecoveryCode(user, normalizedCode))
            {
                return new AuthResult.InvalidTwoFactorCode();
            }
            recoveryCodeUsed = true;
        }

        if (!user.ConsumeTwoFactorChallenge(challengeHash, clock))
        {
            return new AuthResult.InvalidChallengeToken();
        }

        if (recoveryCodeUsed)
        {
            user.DisableTwoFactor(clock);
        }

        await using var uow = await unitOfWork.BeginAsync(cancellationToken);

        try
        {
            await users.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            user.RemoveTwoFactorChallenge(challengeHash, clock);
            return new AuthResult.InvalidChallengeToken();
        }

        var accessToken = accessTokenService.Generate(user.Id.Value, user.Email.Value);
        var refreshToken = await IssueSessionAsync(user.Id, cancellationToken);
        await uow.CommitAsync(cancellationToken);

        return new AuthResult.Success(user.Id.Value, user.Email.Value, accessToken, refreshToken);
    }

    public async Task<AuthResult> RefreshAsync(
        string refreshToken,
        CancellationToken cancellationToken = default
    )
    {
        var hash = TokenHash.From(TokenHasher.Hash(refreshToken));
        var session = await userSessions.GetByTokenHashAsync(hash, cancellationToken);

        if (session is null || !session.IsActive(clock.UtcNow))
        {
            if (session is not null)
            {
                await userSessions.RevokeAllForUserAsync(session.UserId, cancellationToken);
            }
            return new AuthResult.InvalidRefreshToken();
        }

        await using var uow = await unitOfWork.BeginAsync(cancellationToken);

        if (!session.TryRevoke(clock))
        {
            await userSessions.RevokeAllForUserAsync(session.UserId, cancellationToken);
            await uow.CommitAsync(cancellationToken);
            return new AuthResult.InvalidRefreshToken();
        }

        try
        {
            await userSessions.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            await userSessions.RevokeAllForUserAsync(session.UserId, cancellationToken);
            await uow.CommitAsync(cancellationToken);
            return new AuthResult.InvalidRefreshToken();
        }

        var user = await users.GetByIdAsync(session.UserId, cancellationToken);
        if (user is null)
        {
            await uow.CommitAsync(cancellationToken);
            return new AuthResult.InvalidRefreshToken();
        }

        if (!user.EmailVerified)
        {
            await userSessions.RevokeAllForUserAsync(user.Id, cancellationToken);
            await uow.CommitAsync(cancellationToken);
            return new AuthResult.EmailNotVerified();
        }

        var newRefreshToken = await IssueSessionAsync(user.Id, cancellationToken);
        await uow.CommitAsync(cancellationToken);

        var accessToken = accessTokenService.Generate(user.Id.Value, user.Email.Value);
        return new AuthResult.Success(user.Id.Value, user.Email.Value, accessToken, newRefreshToken);
    }

    public async Task<AuthResult> VerifyEmailAsync(
        string token,
        CancellationToken cancellationToken = default
    )
    {
        var hash = TokenHash.From(TokenHasher.Hash(token));
        var user = await users.GetByEmailVerificationTokenHashAsync(hash, cancellationToken);
        if (user is null)
        {
            return new AuthResult.InvalidVerificationToken();
        }

        var stored = user.EmailVerificationTokens.SingleOrDefault(t => t.TokenHash == hash);
        if (stored is null || stored.IsConsumed)
        {
            return new AuthResult.InvalidVerificationToken();
        }

        if (stored.IsExpired(clock.UtcNow))
        {
            return new AuthResult.VerificationTokenExpired();
        }

        await using var uow = await unitOfWork.BeginAsync(cancellationToken);

        if (!user.VerifyEmail(hash, clock))
        {
            return new AuthResult.InvalidVerificationToken();
        }

        try
        {
            await users.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            return new AuthResult.InvalidVerificationToken();
        }

        var refreshToken = await IssueSessionAsync(user.Id, cancellationToken);
        await uow.CommitAsync(cancellationToken);

        var accessToken = accessTokenService.Generate(user.Id.Value, user.Email.Value);
        return new AuthResult.Success(user.Id.Value, user.Email.Value, accessToken, refreshToken);
    }

    public async Task<AuthResult> ResendVerificationAsync(
        string email,
        CancellationToken cancellationToken = default
    )
    {
        var normalizedEmail = Email.Normalize(email);
        var user = await users.GetByEmailAsync(normalizedEmail, cancellationToken);

        if (user is null || user.EmailVerified)
        {
            return new AuthResult.EmailVerificationSent();
        }

        var (plainToken, hashedToken) = secureTokenGenerator.Generate();
        var expiresAt = clock.UtcNow.AddHours(EmailVerificationHours);
        user.IssueEmailVerificationToken(TokenHash.From(hashedToken), expiresAt, clock);
        await users.SaveChangesAsync(cancellationToken);

        await emailWorkflowStarter.StartVerificationWorkflowAsync(
            user.Id.Value,
            user.Email.Value,
            plainToken,
            cancellationToken
        );

        return new AuthResult.EmailVerificationSent();
    }

    public async Task<AuthResult> ForgotPasswordAsync(
        string email,
        CancellationToken cancellationToken = default
    )
    {
        var normalizedEmail = Email.Normalize(email);
        var user = await users.GetByEmailAsync(normalizedEmail, cancellationToken);

        if (user is null || !user.EmailVerified)
        {
            return new AuthResult.PasswordResetSent();
        }

        var (plainToken, hashedToken) = secureTokenGenerator.Generate();
        var expiresAt = clock.UtcNow.AddMinutes(PasswordResetMinutes);
        user.IssuePasswordResetToken(TokenHash.From(hashedToken), expiresAt, clock);
        await users.SaveChangesAsync(cancellationToken);

        await emailWorkflowStarter.StartPasswordResetWorkflowAsync(
            user.Id.Value,
            user.Email.Value,
            plainToken,
            cancellationToken
        );

        return new AuthResult.PasswordResetSent();
    }

    public async Task<AuthResult> ResetPasswordAsync(
        string token,
        string newPassword,
        CancellationToken cancellationToken = default
    )
    {
        var hash = TokenHash.From(TokenHasher.Hash(token));
        var user = await users.GetByPasswordResetTokenHashAsync(hash, cancellationToken);
        if (user is null)
        {
            return new AuthResult.InvalidResetToken();
        }

        var stored = user.PasswordResetTokens.SingleOrDefault(t => t.TokenHash == hash);
        if (stored is null || stored.IsConsumed)
        {
            return new AuthResult.InvalidResetToken();
        }

        if (stored.IsExpired(clock.UtcNow))
        {
            return new AuthResult.ResetTokenExpired();
        }

        var newPasswordHash = PasswordHash.From(passwordHasher.Hash(newPassword));

        await using var uow = await unitOfWork.BeginAsync(cancellationToken);

        if (!user.ResetPassword(hash, newPasswordHash, clock))
        {
            return new AuthResult.InvalidResetToken();
        }

        try
        {
            await users.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            return new AuthResult.InvalidResetToken();
        }

        await userSessions.RevokeAllForUserAsync(user.Id, cancellationToken);
        var refreshToken = await IssueSessionAsync(user.Id, cancellationToken);
        await uow.CommitAsync(cancellationToken);

        var accessToken = accessTokenService.Generate(user.Id.Value, user.Email.Value);
        return new AuthResult.Success(user.Id.Value, user.Email.Value, accessToken, refreshToken);
    }

    private async Task<string> IssueSessionAsync(UserId userId, CancellationToken cancellationToken)
    {
        var (plainToken, hashedToken) = secureTokenGenerator.Generate();
        var expiresAt = clock.UtcNow.AddDays(RefreshTokenDays);
        var session = UserSession.Issue(userId, TokenHash.From(hashedToken), expiresAt, clock);
        await userSessions.AddAsync(session, cancellationToken);
        await userSessions.SaveChangesAsync(cancellationToken);
        return plainToken;
    }

    private bool VerifyTotpCode(User user, string code)
    {
        if (user.Totp is null || !user.Totp.IsVerified)
        {
            return false;
        }

        var secret = totpSecretProtector.Unprotect(user.Totp.EncryptedSecret.Value);
        var result = totpService.Verify(secret, code);
        return result is TotpVerificationResult.Match match
            && user.VerifyAndAdvanceTotp(match.Step, clock);
    }

    private bool ConsumeRecoveryCode(User user, string code)
    {
        var recoveryCode = user.RecoveryCodes
            .Where(c => !c.IsConsumed)
            .FirstOrDefault(c => passwordHasher.Verify(code, c.CodeHash.Value));

        return recoveryCode is not null && user.ConsumeRecoveryCode(recoveryCode.Id, clock);
    }

    private static bool IsLikelyTotpCode(string code) =>
        code.Length == 6 && code.All(char.IsDigit);
}
