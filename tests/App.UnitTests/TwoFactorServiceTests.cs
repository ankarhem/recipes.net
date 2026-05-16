using App;
using App.Identity;
using App.Identity.Ports;
using AwesomeAssertions;
using Domain;
using Domain.Identity;
using NSubstitute;
using NSubstitute.Core;
using Xunit;

namespace App.UnitTests;

public class TwoFactorServiceTests
{
    private static readonly DateTimeOffset TestNow = new(2026, 5, 14, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task SetupAsync_WhenUserNotFound_ReturnsUserNotFound()
    {
        var ctx = CreateSut();
        GivenUserById(ctx, null);

        var result = await ctx.Sut.SetupAsync(UserId.New());

        result.Should().BeOfType<TwoFactorResult.UserNotFound>();
        await ctx.UnitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task SetupAsync_WhenAlreadyEnabled_ReturnsAlreadyEnabled()
    {
        var ctx = CreateSut();
        var user = CreateEnabledUser(ctx.Clock);
        GivenUserById(ctx, user);

        var result = await ctx.Sut.SetupAsync(user.Id);

        result.Should().BeOfType<TwoFactorResult.AlreadyEnabled>();
        ctx.TotpService.DidNotReceiveWithAnyArgs().GenerateSecret();
        await ctx.UnitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task SetupAsync_WhenFreshUser_ReturnsSetupPendingAndPersistsPendingCredential()
    {
        var ctx = CreateSut();
        var user = CreateUser(ctx.Clock);
        var secret = new byte[] { 1, 2, 3, 4 };
        var qrPng = new byte[] { 9, 8, 7 };
        GivenUserById(ctx, user);
        ctx.TotpService.GenerateSecret().Returns(secret);
        ctx.TotpService.EncodeBase32(secret).Returns("BASE32SECRET");
        ctx.TotpService.BuildOtpAuthUri(secret, user.Email.Value, "recipes").Returns("otpauth-uri");
        ctx.QrGenerator.GeneratePng("otpauth-uri").Returns(qrPng);
        ctx.TotpSecretProtector.Protect(secret).Returns("protected-secret");

        var result = await ctx.Sut.SetupAsync(user.Id);

        var pending = result.Should().BeOfType<TwoFactorResult.SetupPending>().Subject;
        pending.Base32Secret.Should().Be("BASE32SECRET");
        pending.OtpAuthUri.Should().Be("otpauth-uri");
        pending.QrCodePngBase64.Should().Be(Convert.ToBase64String(qrPng));
        user.Totp.Should().NotBeNull();
        user.Totp!.EncryptedSecret.Value.Should().Be("protected-secret");
        user.Totp.IsVerified.Should().BeFalse();
        await ctx.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ConfirmAsync_WhenAlreadyEnabled_ReturnsAlreadyEnabled()
    {
        var ctx = CreateSut();
        var user = CreateEnabledUser(ctx.Clock);
        GivenUserById(ctx, user);

        var result = await ctx.Sut.ConfirmAsync(user.Id, "123456");

        result.Should().BeOfType<TwoFactorResult.AlreadyEnabled>();
        ctx.TotpService.DidNotReceiveWithAnyArgs().Verify(default!, default!);
        await ctx.UnitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task ConfirmAsync_WhenNoPending_ReturnsNotEnabled()
    {
        var ctx = CreateSut();
        var user = CreateUser(ctx.Clock);
        GivenUserById(ctx, user);

        var result = await ctx.Sut.ConfirmAsync(user.Id, "123456");

        result.Should().BeOfType<TwoFactorResult.NotEnabled>();
        await ctx.UnitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task ConfirmAsync_WithInvalidCode_ReturnsInvalidCode()
    {
        var ctx = CreateSut();
        var user = CreatePendingUser(ctx.Clock);
        var secret = GivenProtectedSecret(ctx);
        GivenUserById(ctx, user);
        ctx.TotpService.Verify(secret, "123456").Returns(new TotpVerificationResult.NoMatch());

        var result = await ctx.Sut.ConfirmAsync(user.Id, "123456");

        result.Should().BeOfType<TwoFactorResult.InvalidCode>();
        user.HasTwoFactorEnabled.Should().BeFalse();
        await ctx.UnitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task ConfirmAsync_WithValidCode_ReturnsEnabledWithTenRecoveryCodes()
    {
        var ctx = CreateSut();
        var user = CreatePendingUser(ctx.Clock);
        var secret = GivenProtectedSecret(ctx);
        var recoveryCodes = CreateRecoveryCodes();
        GivenUserById(ctx, user);
        ctx.TotpService.Verify(secret, "123456").Returns(new TotpVerificationResult.Match(123));
        ctx.RecoveryCodeGenerator.Generate().Returns(recoveryCodes);

        var result = await ctx.Sut.ConfirmAsync(user.Id, "123456");

        var enabled = result.Should().BeOfType<TwoFactorResult.Enabled>().Subject;
        enabled.RecoveryCodes.Should().Equal(recoveryCodes);
        user.HasTwoFactorEnabled.Should().BeTrue();
        user.Totp!.LastUsedStep.Should().Be(123);
        user.RecoveryCodes.Should().HaveCount(10);
        user.RecoveryCodes
            .Select(c => c.CodeHash.Value)
            .Should()
            .BeEquivalentTo(recoveryCodes.Select(code => $"hashed-{code}"));
        await ctx.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DisableAsync_WhenNotEnabled_ReturnsNotEnabled()
    {
        var ctx = CreateSut();
        var user = CreateUser(ctx.Clock);
        GivenUserById(ctx, user);

        var result = await ctx.Sut.DisableAsync(user.Id, "123456");

        result.Should().BeOfType<TwoFactorResult.NotEnabled>();
        await ctx.UnitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task DisableAsync_WithInvalidCode_ReturnsInvalidCode()
    {
        var ctx = CreateSut();
        var user = CreateEnabledUser(ctx.Clock);
        var secret = GivenProtectedSecret(ctx);
        GivenUserById(ctx, user);
        ctx.TotpService.Verify(secret, "123456").Returns(new TotpVerificationResult.NoMatch());

        var result = await ctx.Sut.DisableAsync(user.Id, "123456");

        result.Should().BeOfType<TwoFactorResult.InvalidCode>();
        user.HasTwoFactorEnabled.Should().BeTrue();
        await ctx.UnitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
        await ctx.UnitOfWorkScope.DidNotReceiveWithAnyArgs().CommitAsync(default);
    }

    [Fact]
    public async Task DisableAsync_WithValidCode_ClearsTotpAndCodes()
    {
        var ctx = CreateSut();
        var user = CreateEnabledUser(ctx.Clock);
        var secret = GivenProtectedSecret(ctx);
        GivenUserById(ctx, user);
        ctx.TotpService.Verify(secret, "123456").Returns(new TotpVerificationResult.Match(101));

        var result = await ctx.Sut.DisableAsync(user.Id, "123456");

        result.Should().BeOfType<TwoFactorResult.Disabled>();
        user.HasTwoFactorEnabled.Should().BeFalse();
        user.Totp.Should().BeNull();
        user.RecoveryCodes.Should().BeEmpty();
        await ctx.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await ctx.UnitOfWorkScope.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegenerateRecoveryCodesAsync_WithInvalidCode_ReturnsInvalidCode()
    {
        var ctx = CreateSut();
        var user = CreateEnabledUser(ctx.Clock);
        var secret = GivenProtectedSecret(ctx);
        GivenUserById(ctx, user);
        ctx.TotpService.Verify(secret, "123456").Returns(new TotpVerificationResult.NoMatch());

        var result = await ctx.Sut.RegenerateRecoveryCodesAsync(user.Id, "123456");

        result.Should().BeOfType<TwoFactorResult.InvalidCode>();
        user.RecoveryCodes.Should().HaveCount(2);
        await ctx.UnitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task RegenerateRecoveryCodesAsync_WithValidCode_ReturnsNewSetOfTen()
    {
        var ctx = CreateSut();
        var user = CreateEnabledUser(ctx.Clock);
        var oldCodeIds = user.RecoveryCodes.Select(c => c.Id).ToArray();
        var secret = GivenProtectedSecret(ctx);
        var recoveryCodes = CreateRecoveryCodes();
        GivenUserById(ctx, user);
        ctx.TotpService.Verify(secret, "123456").Returns(new TotpVerificationResult.Match(101));
        ctx.RecoveryCodeGenerator.Generate().Returns(recoveryCodes);

        var result = await ctx.Sut.RegenerateRecoveryCodesAsync(user.Id, "123456");

        var regenerated = result.Should().BeOfType<TwoFactorResult.CodesRegenerated>().Subject;
        regenerated.RecoveryCodes.Should().Equal(recoveryCodes);
        user.RecoveryCodes.Should().HaveCount(10);
        user.RecoveryCodes.Select(c => c.Id).Should().NotIntersectWith(oldCodeIds);
        user.RecoveryCodes
            .Select(c => c.CodeHash.Value)
            .Should()
            .BeEquivalentTo(recoveryCodes.Select(code => $"hashed-{code}"));
        await ctx.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private static SutContext CreateSut(FakeClock? clock = null)
    {
        clock ??= new FakeClock(TestNow);
        var userRepository = Substitute.For<IUserRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var unitOfWorkScope = Substitute.For<IUnitOfWorkScope>();
        var totpService = Substitute.For<ITotpService>();
        var totpSecretProtector = Substitute.For<ITotpSecretProtector>();
        var qrGenerator = Substitute.For<IQrCodeGenerator>();
        var recoveryCodeGenerator = Substitute.For<IRecoveryCodeGenerator>();
        var passwordHasher = Substitute.For<IPasswordHasher>();

        userRepository
            .GetByIdAsync(default, default)
            .ReturnsForAnyArgs(
                (Func<CallInfo, Task<User?>>)(_ => Task.FromResult<User?>(null))
            );
        unitOfWork
            .BeginAsync(default)
            .ReturnsForAnyArgs(
                (Func<CallInfo, Task<IUnitOfWorkScope>>)(_ => Task.FromResult(unitOfWorkScope))
            );
        unitOfWork.SaveChangesAsync(default).ReturnsForAnyArgs(Task.CompletedTask);

        totpService
            .Verify(default!, default!)
            .ReturnsForAnyArgs(new TotpVerificationResult.NoMatch());
        totpSecretProtector.Unprotect(default!).ReturnsForAnyArgs([]);
        recoveryCodeGenerator.Generate().Returns(CreateRecoveryCodes());
        passwordHasher
            .Hash(default!)
            .ReturnsForAnyArgs((Func<CallInfo, string>)(call => $"hashed-{call.Arg<string>()}"));

        var sut = new TwoFactorService(
            userRepository,
            unitOfWork,
            totpService,
            totpSecretProtector,
            qrGenerator,
            recoveryCodeGenerator,
            passwordHasher,
            clock
        );

        return new SutContext(
            sut,
            userRepository,
            unitOfWork,
            unitOfWorkScope,
            totpService,
            totpSecretProtector,
            qrGenerator,
            recoveryCodeGenerator,
            passwordHasher,
            clock
        );
    }

    private static void GivenUserById(SutContext ctx, User? user) =>
        ctx.UserRepository
            .GetByIdAsync(default, default)
            .ReturnsForAnyArgs((Func<CallInfo, Task<User?>>)(_ => Task.FromResult(user)));

    private static byte[] GivenProtectedSecret(SutContext ctx)
    {
        var secret = new byte[] { 1, 2, 3, 4 };
        ctx.TotpSecretProtector.Unprotect("protected-secret").Returns(secret);
        return secret;
    }

    private static User CreateEnabledUser(FakeClock clock)
    {
        var user = CreatePendingUser(clock);
        user.ConfirmTwoFactor(
                100,
                [RecoveryCodeHash.From("old-code-1"), RecoveryCodeHash.From("old-code-2")],
                clock
            )
            .Should()
            .BeTrue();
        return user;
    }

    private static User CreatePendingUser(FakeClock clock)
    {
        var user = CreateUser(clock);
        user.StartTwoFactorSetup(EncryptedTotpSecret.From("protected-secret"), clock);
        return user;
    }

    private static User CreateUser(IClock clock) =>
        User.Register(Email.Normalize("test@example.com"), PasswordHash.From("hashed-password"), clock);

    private static IReadOnlyList<string> CreateRecoveryCodes() =>
        Enumerable.Range(1, 10).Select(i => $"CODE-{i:0000}-TEST").ToArray();

    private sealed record SutContext(
        TwoFactorService Sut,
        IUserRepository UserRepository,
        IUnitOfWork UnitOfWork,
        IUnitOfWorkScope UnitOfWorkScope,
        ITotpService TotpService,
        ITotpSecretProtector TotpSecretProtector,
        IQrCodeGenerator QrGenerator,
        IRecoveryCodeGenerator RecoveryCodeGenerator,
        IPasswordHasher PasswordHasher,
        FakeClock Clock
    );
}
