using App;
using App.Identity;
using App.Identity.Ports;
using App.Identity.Workflows;
using AwesomeAssertions;
using Domain;
using Domain.Identity;
using NSubstitute;
using NSubstitute.Core;
using Xunit;

namespace App.UnitTests;

public class AuthServiceTests
{
    private static readonly DateTimeOffset TestNow = new(2026, 5, 14, 12, 0, 0, TimeSpan.Zero);
    private static readonly AccessToken TestAccessToken = new()
    {
        Token = "access-token",
        ExpiresAt = TestNow.AddMinutes(15),
    };

    [Fact]
    public async Task RegisterAsync_NewEmail_ReturnsRegistrationPendingAndStartsVerificationWorkflow()
    {
        var ctx = CreateSut();
        User? addedUser = null;
        GivenUserByEmail(ctx, null);
        ctx.PasswordHasher.Hash("password").Returns("hashed-password");
        ctx.SecureTokenGenerator.Generate().Returns(("verification-token", "hashed-verification-token"));
        ctx.UserRepository
            .When(x => x.AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>()))
            .Do(call => addedUser = call.Arg<User>());

        var result = await ctx.Sut.RegisterAsync(" Test@Example.COM ", "password");

        addedUser.Should().NotBeNull();
        addedUser!.Email.Value.Should().Be("test@example.com");
        addedUser.PasswordHash.Value.Should().Be("hashed-password");
        addedUser.EmailVerified.Should().BeFalse();
        var verificationToken = addedUser.EmailVerificationTokens.Should().ContainSingle().Which;
        verificationToken.TokenHash.Should().Be(TokenHash.From("hashed-verification-token"));
        verificationToken.ExpiresAt.Should().Be(ctx.Clock.UtcNow.AddHours(24));

        var pending = result.Should().BeOfType<AuthResult.RegistrationPending>().Subject;
        pending.UserId.Should().Be(addedUser.Id.Value);
        pending.Email.Should().Be("test@example.com");

        await ctx.UserRepository
            .Received(1)
            .GetByEmailAsync(
                Arg.Is<Email>(email => email.Value == "test@example.com"),
                Arg.Any<CancellationToken>()
            );
        await ctx.UserRepository.Received(1).AddAsync(addedUser, Arg.Any<CancellationToken>());
        await ctx.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await ctx.EmailWorkflowStarter
            .Received(1)
            .StartVerificationWorkflowAsync(
                addedUser.Id.Value,
                "test@example.com",
                "verification-token",
                Arg.Any<CancellationToken>()
            );
        ctx.AccessTokenService.DidNotReceiveWithAnyArgs().Generate(default, default!);
        await ctx.UserSessionRepository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task RegisterAsync_DuplicateEmail_ReturnsEmailAlreadyRegistered()
    {
        var ctx = CreateSut();
        var existingUser = CreateVerifiedUser(ctx.Clock);
        GivenUserByEmail(ctx, existingUser);

        var result = await ctx.Sut.RegisterAsync("test@example.com", "password");

        result.Should().BeOfType<AuthResult.EmailAlreadyRegistered>();
        ctx.PasswordHasher.DidNotReceiveWithAnyArgs().Hash(default!);
        ctx.SecureTokenGenerator.DidNotReceiveWithAnyArgs().Generate();
        await ctx.UserRepository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
        await ctx.UnitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
        await ctx.EmailWorkflowStarter
            .DidNotReceiveWithAnyArgs()
            .StartVerificationWorkflowAsync(default, default!, default!, default);
    }

    [Fact]
    public async Task LoginAsync_WhenUserHasNoTwoFactor_ReturnsSuccessAsBefore()
    {
        var ctx = CreateSut();
        var user = CreateVerifiedUser(ctx.Clock);
        GivenUserByEmail(ctx, user);
        ctx.PasswordHasher.Verify("password", user.PasswordHash.Value).Returns(true);
        ctx.SecureTokenGenerator.Generate().Returns(("refresh-token", "hashed-refresh-token"));

        var result = await ctx.Sut.LoginAsync(" Test@Example.COM ", "password");

        var success = result.Should().BeOfType<AuthResult.Success>().Subject;
        success.UserId.Should().Be(user.Id.Value);
        success.Email.Should().Be("test@example.com");
        success.AccessToken.Should().Be(TestAccessToken);
        success.RefreshToken.Should().Be("refresh-token");
        ctx.PasswordHasher.Received(1).Verify("password", "hashed-password");
        ctx.AccessTokenService.Received(1).Generate(user.Id.Value, user.Email.Value);
        await ctx.UserSessionRepository
            .Received(1)
            .AddAsync(
                Arg.Is<UserSession>(session =>
                    IsIssuedSession(session, user.Id, "hashed-refresh-token", ctx.Clock.UtcNow)
                ),
                Arg.Any<CancellationToken>()
            );
        await ctx.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoginAsync_WhenUserHasTwoFactorEnabled_ReturnsTwoFactorRequiredAndIssuesChallenge()
    {
        var ctx = CreateSut();
        var user = CreateTwoFactorEnabledUser(ctx.Clock);
        GivenUserByEmail(ctx, user);
        ctx.PasswordHasher.Verify("password", user.PasswordHash.Value).Returns(true);
        ctx.SecureTokenGenerator.Generate().Returns(("challenge-token", "challenge-hash"));

        var result = await ctx.Sut.LoginAsync("test@example.com", "password");

        var required = result.Should().BeOfType<AuthResult.TwoFactorRequired>().Subject;
        required.UserId.Should().Be(user.Id.Value);
        required.ChallengeToken.Should().Be("challenge-token");
        required.AvailableMethods.Should().Equal("totp");
        var challenge = user.TwoFactorChallenges.Should().ContainSingle().Which;
        challenge.TokenHash.Should().Be(TokenHash.From("challenge-hash"));
        challenge.ExpiresAt.Should().Be(ctx.Clock.UtcNow.AddMinutes(5));
        await ctx.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        ctx.AccessTokenService.DidNotReceiveWithAnyArgs().Generate(default, default!);
        await ctx.UserSessionRepository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task VerifyTotpAsync_WithUnknownChallenge_ReturnsInvalidChallengeToken()
    {
        var ctx = CreateSut();
        GivenUserByTwoFactorChallenge(ctx, null);

        var result = await ctx.Sut.VerifyTotpAsync("missing-challenge", "123456");

        result.Should().BeOfType<AuthResult.InvalidChallengeToken>();
        await ctx.UserRepository
            .Received(1)
            .GetByTwoFactorChallengeHashAsync(
                TokenHash.FromPlain("missing-challenge"),
                Arg.Any<CancellationToken>()
            );
        await ctx.UnitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
        await ctx.UserSessionRepository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task VerifyTotpAsync_WithExpiredChallenge_ReturnsChallengeTokenExpired()
    {
        var ctx = CreateSut();
        var user = CreateTwoFactorEnabledUser(ctx.Clock);
        var challengeHash = IssueTwoFactorChallenge(user, "expired-challenge", ctx.Clock, TimeSpan.FromMinutes(-1));
        GivenUserByTwoFactorChallenge(ctx, user);

        var result = await ctx.Sut.VerifyTotpAsync("expired-challenge", "123456");

        result.Should().BeOfType<AuthResult.ChallengeTokenExpired>();
        user.TwoFactorChallenges.Single(c => c.TokenHash == challengeHash).ConsumedAt.Should().BeNull();
        await ctx.UnitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
        await ctx.UserSessionRepository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task VerifyTotpAsync_WithConsumedChallenge_ReturnsInvalidChallengeToken()
    {
        var ctx = CreateSut();
        var user = CreateTwoFactorEnabledUser(ctx.Clock);
        var challengeHash = IssueTwoFactorChallenge(user, "consumed-challenge", ctx.Clock, TimeSpan.FromMinutes(5));
        user.ConsumeTwoFactorChallenge(challengeHash, ctx.Clock).Should().BeTrue();
        GivenUserByTwoFactorChallenge(ctx, user);

        var result = await ctx.Sut.VerifyTotpAsync("consumed-challenge", "123456");

        result.Should().BeOfType<AuthResult.InvalidChallengeToken>();
        await ctx.UnitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
        await ctx.UserSessionRepository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task VerifyTotpAsync_WithValidTotpCode_ReturnsSuccessAndIssuesSession()
    {
        var ctx = CreateSut();
        var user = CreateTwoFactorEnabledUser(ctx.Clock);
        var challengeHash = IssueTwoFactorChallenge(user, "challenge-token", ctx.Clock, TimeSpan.FromMinutes(5));
        var secret = GivenTotpSecret(ctx);
        GivenUserByTwoFactorChallenge(ctx, user);
        ctx.TotpService.Verify(secret, "123456").Returns(new TotpVerificationResult.Match(101));
        ctx.SecureTokenGenerator.Generate().Returns(("refresh-token", "hashed-refresh-token"));

        var result = await ctx.Sut.VerifyTotpAsync("challenge-token", "123456");

        var success = result.Should().BeOfType<AuthResult.Success>().Subject;
        success.UserId.Should().Be(user.Id.Value);
        success.Email.Should().Be(user.Email.Value);
        success.AccessToken.Should().Be(TestAccessToken);
        success.RefreshToken.Should().Be("refresh-token");
        user.Totp!.LastUsedStep.Should().Be(101);
        user.TwoFactorChallenges.Single(c => c.TokenHash == challengeHash).ConsumedAt.Should().Be(ctx.Clock.UtcNow);
        await ctx.UnitOfWork.Received(2).SaveChangesAsync(Arg.Any<CancellationToken>());
        await ctx.UserSessionRepository
            .Received(1)
            .AddAsync(
                Arg.Is<UserSession>(session =>
                    IsIssuedSession(session, user.Id, "hashed-refresh-token", ctx.Clock.UtcNow)
                ),
                Arg.Any<CancellationToken>()
            );
        await ctx.UnitOfWorkScope.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task VerifyTotpAsync_WithReplayedTotpStep_ReturnsInvalidTwoFactorCode()
    {
        var ctx = CreateSut();
        var user = CreateTwoFactorEnabledUser(ctx.Clock);
        IssueTwoFactorChallenge(user, "challenge-token", ctx.Clock, TimeSpan.FromMinutes(5));
        var secret = GivenTotpSecret(ctx);
        GivenUserByTwoFactorChallenge(ctx, user);
        ctx.TotpService.Verify(secret, "123456").Returns(new TotpVerificationResult.Match(100));

        var result = await ctx.Sut.VerifyTotpAsync("challenge-token", "123456");

        result.Should().BeOfType<AuthResult.InvalidTwoFactorCode>();
        user.Totp!.LastUsedStep.Should().Be(100);
        user.TwoFactorChallenges.Single().ConsumedAt.Should().BeNull();
        await ctx.UnitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
        await ctx.UserSessionRepository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task VerifyTotpAsync_WithInvalidTotpCode_ReturnsInvalidTwoFactorCode()
    {
        var ctx = CreateSut();
        var user = CreateTwoFactorEnabledUser(ctx.Clock);
        IssueTwoFactorChallenge(user, "challenge-token", ctx.Clock, TimeSpan.FromMinutes(5));
        var secret = GivenTotpSecret(ctx);
        GivenUserByTwoFactorChallenge(ctx, user);
        ctx.TotpService.Verify(secret, "123456").Returns(new TotpVerificationResult.NoMatch());

        var result = await ctx.Sut.VerifyTotpAsync("challenge-token", "123456");

        result.Should().BeOfType<AuthResult.InvalidTwoFactorCode>();
        user.TwoFactorChallenges.Single().ConsumedAt.Should().BeNull();
        await ctx.UnitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
        await ctx.UserSessionRepository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task VerifyTotpAsync_WithValidRecoveryCode_DisablesTwoFactorAndIssuesSession()
    {
        var ctx = CreateSut();
        var user = CreateTwoFactorEnabledUser(
            ctx.Clock,
            recoveryCodeHashes: ["hashed-recovery-code-1", "hashed-recovery-code-2"]
        );
        IssueTwoFactorChallenge(user, "challenge-token", ctx.Clock, TimeSpan.FromMinutes(5));
        GivenUserByTwoFactorChallenge(ctx, user);
        ctx.PasswordHasher.Verify("ABCD-EFGH-JKMP", "hashed-recovery-code-1").Returns(true);
        ctx.SecureTokenGenerator.Generate().Returns(("refresh-token", "hashed-refresh-token"));

        var result = await ctx.Sut.VerifyTotpAsync("challenge-token", "ABCD-EFGH-JKMP");

        var success = result.Should().BeOfType<AuthResult.Success>().Subject;
        success.UserId.Should().Be(user.Id.Value);
        success.RefreshToken.Should().Be("refresh-token");
        user.HasTwoFactorEnabled.Should().BeFalse();
        user.Totp.Should().BeNull();
        user.RecoveryCodes.Should().BeEmpty();
        await ctx.UnitOfWork.Received(2).SaveChangesAsync(Arg.Any<CancellationToken>());
        await ctx.UserSessionRepository.Received(1).AddAsync(Arg.Any<UserSession>(), Arg.Any<CancellationToken>());
        await ctx.UnitOfWorkScope.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task VerifyTotpAsync_WithConsumedRecoveryCode_ReturnsInvalidTwoFactorCode()
    {
        var ctx = CreateSut();
        var user = CreateTwoFactorEnabledUser(ctx.Clock, recoveryCodeHashes: ["hashed-recovery-code"]);
        IssueTwoFactorChallenge(user, "challenge-token", ctx.Clock, TimeSpan.FromMinutes(5));
        var recoveryCode = user.RecoveryCodes.Single();
        user.ConsumeRecoveryCode(recoveryCode.Id, ctx.Clock).Should().BeTrue();
        GivenUserByTwoFactorChallenge(ctx, user);
        ctx.PasswordHasher.Verify("ABCD-EFGH-JKMP", "hashed-recovery-code").Returns(true);

        var result = await ctx.Sut.VerifyTotpAsync("challenge-token", "ABCD-EFGH-JKMP");

        result.Should().BeOfType<AuthResult.InvalidTwoFactorCode>();
        user.TwoFactorChallenges.Single().ConsumedAt.Should().BeNull();
        await ctx.UnitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
        await ctx.UserSessionRepository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task VerifyTotpAsync_WithUnknownRecoveryCode_ReturnsInvalidTwoFactorCode()
    {
        var ctx = CreateSut();
        var user = CreateTwoFactorEnabledUser(ctx.Clock, recoveryCodeHashes: ["hashed-recovery-code"]);
        IssueTwoFactorChallenge(user, "challenge-token", ctx.Clock, TimeSpan.FromMinutes(5));
        GivenUserByTwoFactorChallenge(ctx, user);
        ctx.PasswordHasher.Verify("ABCD-EFGH-JKMP", "hashed-recovery-code").Returns(false);

        var result = await ctx.Sut.VerifyTotpAsync("challenge-token", "ABCD-EFGH-JKMP");

        result.Should().BeOfType<AuthResult.InvalidTwoFactorCode>();
        user.RecoveryCodes.Single().IsConsumed.Should().BeFalse();
        user.TwoFactorChallenges.Single().ConsumedAt.Should().BeNull();
        await ctx.UnitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
        await ctx.UserSessionRepository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task LoginAsync_UnknownEmail_ReturnsInvalidCredentials()
    {
        var ctx = CreateSut();
        GivenUserByEmail(ctx, null);

        var result = await ctx.Sut.LoginAsync("missing@example.com", "password");

        result.Should().BeOfType<AuthResult.InvalidCredentials>();
        ctx.PasswordHasher.Received(1).Verify("password", "dummy-hash");
        ctx.AccessTokenService.DidNotReceiveWithAnyArgs().Generate(default, default!);
        ctx.SecureTokenGenerator.DidNotReceiveWithAnyArgs().Generate();
        await ctx.UserSessionRepository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_ReturnsInvalidCredentials()
    {
        var ctx = CreateSut();
        var user = CreateVerifiedUser(ctx.Clock);
        GivenUserByEmail(ctx, user);
        ctx.PasswordHasher.Verify("wrong-password", user.PasswordHash.Value).Returns(false);

        var result = await ctx.Sut.LoginAsync("test@example.com", "wrong-password");

        result.Should().BeOfType<AuthResult.InvalidCredentials>();
        ctx.PasswordHasher.Received(1).Verify("wrong-password", "hashed-password");
        ctx.AccessTokenService.DidNotReceiveWithAnyArgs().Generate(default, default!);
        ctx.SecureTokenGenerator.DidNotReceiveWithAnyArgs().Generate();
        await ctx.UserSessionRepository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task LoginAsync_UnverifiedUser_ReturnsEmailNotVerified()
    {
        var ctx = CreateSut();
        var user = CreateUnverifiedUser(ctx.Clock);
        GivenUserByEmail(ctx, user);
        ctx.PasswordHasher.Verify("password", user.PasswordHash.Value).Returns(true);

        var result = await ctx.Sut.LoginAsync("test@example.com", "password");

        result.Should().BeOfType<AuthResult.EmailNotVerified>();
        ctx.PasswordHasher.Received(1).Verify("password", "hashed-password");
        ctx.AccessTokenService.DidNotReceiveWithAnyArgs().Generate(default, default!);
        ctx.SecureTokenGenerator.DidNotReceiveWithAnyArgs().Generate();
        await ctx.UserSessionRepository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task RefreshAsync_ValidActiveSession_RotatesSessionAndReturnsSuccess()
    {
        var ctx = CreateSut();
        var user = CreateVerifiedUser(ctx.Clock);
        var session = CreateSession(user.Id, "refresh-token", ctx.Clock);
        GivenSessionByRefreshToken(ctx, session);
        GivenUserById(ctx, user);
        ctx.SecureTokenGenerator.Generate().Returns(("new-refresh-token", "new-hashed-token"));

        var result = await ctx.Sut.RefreshAsync("refresh-token");

        var success = result.Should().BeOfType<AuthResult.Success>().Subject;
        success.UserId.Should().Be(user.Id.Value);
        success.Email.Should().Be(user.Email.Value);
        success.AccessToken.Should().Be(TestAccessToken);
        success.RefreshToken.Should().Be("new-refresh-token");
        session.IsRevoked.Should().BeTrue();
        session.RevokedAt.Should().Be(ctx.Clock.UtcNow);
        await ctx.UnitOfWork.Received(2).SaveChangesAsync(Arg.Any<CancellationToken>());
        await ctx.UserSessionRepository
            .Received(1)
            .AddAsync(
                Arg.Is<UserSession>(newSession =>
                    IsIssuedSession(newSession, user.Id, "new-hashed-token", ctx.Clock.UtcNow)
                ),
                Arg.Any<CancellationToken>()
            );
        ctx.AccessTokenService.Received(1).Generate(user.Id.Value, user.Email.Value);
        await ctx.UnitOfWorkScope.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshAsync_UnknownToken_ReturnsInvalidRefreshToken()
    {
        var ctx = CreateSut();
        GivenSessionByRefreshToken(ctx, null);

        var result = await ctx.Sut.RefreshAsync("missing-refresh-token");

        result.Should().BeOfType<AuthResult.InvalidRefreshToken>();
        await ctx.UserSessionRepository
            .Received(1)
            .GetByTokenHashAsync(
                TokenHash.FromPlain("missing-refresh-token"),
                Arg.Any<CancellationToken>()
            );
        await ctx.UserSessionRepository
            .DidNotReceiveWithAnyArgs()
            .RevokeAllForUserAsync(default, default);
        await ctx.UnitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
        await ctx.UserSessionRepository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task RefreshAsync_ExpiredSession_RevokesAllAndReturnsInvalid()
    {
        var ctx = CreateSut();
        var user = CreateVerifiedUser(ctx.Clock);
        var session = CreateSession(
            user.Id,
            "expired-refresh-token",
            ctx.Clock,
            expiresAt: ctx.Clock.UtcNow.AddMinutes(-1)
        );
        GivenSessionByRefreshToken(ctx, session);

        var result = await ctx.Sut.RefreshAsync("expired-refresh-token");

        result.Should().BeOfType<AuthResult.InvalidRefreshToken>();
        await ctx.UserSessionRepository
            .Received(1)
            .RevokeAllForUserAsync(user.Id, Arg.Any<CancellationToken>());
        session.IsRevoked.Should().BeFalse();
        await ctx.UnitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
        await ctx.UserRepository.DidNotReceiveWithAnyArgs().GetByIdAsync(default, default);
        ctx.AccessTokenService.DidNotReceiveWithAnyArgs().Generate(default, default!);
        ctx.SecureTokenGenerator.DidNotReceiveWithAnyArgs().Generate();
    }

    [Fact]
    public async Task RefreshAsync_RevokedSession_RevokesAllAndReturnsInvalid()
    {
        var ctx = CreateSut();
        var user = CreateVerifiedUser(ctx.Clock);
        var session = CreateSession(user.Id, "revoked-refresh-token", ctx.Clock);
        session.TryRevoke(ctx.Clock).Should().BeTrue();
        GivenSessionByRefreshToken(ctx, session);

        var result = await ctx.Sut.RefreshAsync("revoked-refresh-token");

        result.Should().BeOfType<AuthResult.InvalidRefreshToken>();
        await ctx.UserSessionRepository
            .Received(1)
            .RevokeAllForUserAsync(user.Id, Arg.Any<CancellationToken>());
        await ctx.UnitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
        await ctx.UserRepository.DidNotReceiveWithAnyArgs().GetByIdAsync(default, default);
        ctx.AccessTokenService.DidNotReceiveWithAnyArgs().Generate(default, default!);
        ctx.SecureTokenGenerator.DidNotReceiveWithAnyArgs().Generate();
    }

    [Fact]
    public async Task RefreshAsync_SaveChangesThrowsConcurrencyConflict_RevokesAllAndReturnsInvalid()
    {
        var ctx = CreateSut();
        var user = CreateVerifiedUser(ctx.Clock);
        var session = CreateSession(user.Id, "refresh-token", ctx.Clock);
        GivenSessionByRefreshToken(ctx, session);
        ctx.UnitOfWork
            .When(x => x.SaveChangesAsync(Arg.Any<CancellationToken>()))
            .Do(_ => throw new ConcurrencyConflictException("test"));

        var result = await ctx.Sut.RefreshAsync("refresh-token");

        result.Should().BeOfType<AuthResult.InvalidRefreshToken>();
        session.IsRevoked.Should().BeTrue();
        await ctx.UserSessionRepository
            .Received(1)
            .RevokeAllForUserAsync(user.Id, Arg.Any<CancellationToken>());
        await ctx.UserRepository.DidNotReceiveWithAnyArgs().GetByIdAsync(default, default);
        ctx.AccessTokenService.DidNotReceiveWithAnyArgs().Generate(default, default!);
        ctx.SecureTokenGenerator.DidNotReceiveWithAnyArgs().Generate();
        await ctx.UnitOfWorkScope.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshAsync_UnverifiedUser_RevokesAllAndReturnsEmailNotVerified()
    {
        var ctx = CreateSut();
        var user = CreateUnverifiedUser(ctx.Clock);
        var session = CreateSession(user.Id, "refresh-token", ctx.Clock);
        GivenSessionByRefreshToken(ctx, session);
        GivenUserById(ctx, user);

        var result = await ctx.Sut.RefreshAsync("refresh-token");

        result.Should().BeOfType<AuthResult.EmailNotVerified>();
        session.IsRevoked.Should().BeTrue();
        await ctx.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await ctx.UserSessionRepository
            .Received(1)
            .RevokeAllForUserAsync(user.Id, Arg.Any<CancellationToken>());
        await ctx.UserSessionRepository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
        ctx.AccessTokenService.DidNotReceiveWithAnyArgs().Generate(default, default!);
        ctx.SecureTokenGenerator.DidNotReceiveWithAnyArgs().Generate();
    }

    [Fact]
    public async Task VerifyEmailAsync_ValidToken_ConsumesTokenIssuesSessionReturnsSuccess()
    {
        var ctx = CreateSut();
        var user = CreateUnverifiedUser(ctx.Clock);
        var verificationHash = TokenHash.FromPlain("verification-token");
        var token = user.IssueEmailVerificationToken(
            verificationHash,
            ctx.Clock.UtcNow.AddHours(1),
            ctx.Clock
        );
        GivenUserByVerificationToken(ctx, user);
        ctx.SecureTokenGenerator.Generate().Returns(("refresh-token", "hashed-refresh-token"));

        var result = await ctx.Sut.VerifyEmailAsync("verification-token");

        var success = result.Should().BeOfType<AuthResult.Success>().Subject;
        success.UserId.Should().Be(user.Id.Value);
        success.Email.Should().Be(user.Email.Value);
        success.AccessToken.Should().Be(TestAccessToken);
        success.RefreshToken.Should().Be("refresh-token");
        user.EmailVerified.Should().BeTrue();
        user.EmailVerifiedAt.Should().Be(ctx.Clock.UtcNow);
        token.ConsumedAt.Should().Be(ctx.Clock.UtcNow);
        await ctx.UnitOfWork.Received(2).SaveChangesAsync(Arg.Any<CancellationToken>());
        await ctx.UserSessionRepository
            .Received(1)
            .AddAsync(
                Arg.Is<UserSession>(session =>
                    IsIssuedSession(session, user.Id, "hashed-refresh-token", ctx.Clock.UtcNow)
                ),
                Arg.Any<CancellationToken>()
            );
        await ctx.UnitOfWorkScope.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task VerifyEmailAsync_UnknownToken_ReturnsInvalidVerificationToken()
    {
        var ctx = CreateSut();
        GivenUserByVerificationToken(ctx, null);

        var result = await ctx.Sut.VerifyEmailAsync("missing-token");

        result.Should().BeOfType<AuthResult.InvalidVerificationToken>();
        await ctx.UserRepository
            .Received(1)
            .GetByEmailVerificationTokenHashAsync(
                TokenHash.FromPlain("missing-token"),
                Arg.Any<CancellationToken>()
            );
        await ctx.UnitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
        await ctx.UserSessionRepository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
        ctx.AccessTokenService.DidNotReceiveWithAnyArgs().Generate(default, default!);
    }

    [Fact]
    public async Task VerifyEmailAsync_ConsumedToken_ReturnsInvalidVerificationToken()
    {
        var ctx = CreateSut();
        var user = CreateUnverifiedUser(ctx.Clock);
        var hash = TokenHash.FromPlain("consumed-token");
        var token = user.IssueEmailVerificationToken(hash, ctx.Clock.UtcNow.AddHours(1), ctx.Clock);
        user.VerifyEmail(hash, ctx.Clock).Should().BeTrue();
        GivenUserByVerificationToken(ctx, user);

        var result = await ctx.Sut.VerifyEmailAsync("consumed-token");

        result.Should().BeOfType<AuthResult.InvalidVerificationToken>();
        token.IsConsumed.Should().BeTrue();
        await ctx.UnitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
        await ctx.UserSessionRepository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
        ctx.AccessTokenService.DidNotReceiveWithAnyArgs().Generate(default, default!);
        ctx.SecureTokenGenerator.DidNotReceiveWithAnyArgs().Generate();
    }

    [Fact]
    public async Task VerifyEmailAsync_ExpiredToken_ReturnsVerificationTokenExpired()
    {
        var ctx = CreateSut();
        var user = CreateUnverifiedUser(ctx.Clock);
        var hash = TokenHash.FromPlain("expired-token");
        var token = user.IssueEmailVerificationToken(hash, ctx.Clock.UtcNow.AddMinutes(-1), ctx.Clock);
        GivenUserByVerificationToken(ctx, user);

        var result = await ctx.Sut.VerifyEmailAsync("expired-token");

        result.Should().BeOfType<AuthResult.VerificationTokenExpired>();
        user.EmailVerified.Should().BeFalse();
        token.ConsumedAt.Should().BeNull();
        await ctx.UnitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
        await ctx.UserSessionRepository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
        ctx.AccessTokenService.DidNotReceiveWithAnyArgs().Generate(default, default!);
        ctx.SecureTokenGenerator.DidNotReceiveWithAnyArgs().Generate();
    }

    [Fact]
    public async Task VerifyEmailAsync_SaveChangesThrowsConcurrencyConflict_ReturnsInvalid()
    {
        var ctx = CreateSut();
        var user = CreateUnverifiedUser(ctx.Clock);
        var hash = TokenHash.FromPlain("race-token");
        user.IssueEmailVerificationToken(hash, ctx.Clock.UtcNow.AddHours(1), ctx.Clock);
        GivenUserByVerificationToken(ctx, user);
        ctx.UnitOfWork
            .When(x => x.SaveChangesAsync(Arg.Any<CancellationToken>()))
            .Do(_ => throw new ConcurrencyConflictException("test"));

        var result = await ctx.Sut.VerifyEmailAsync("race-token");

        result.Should().BeOfType<AuthResult.InvalidVerificationToken>();
        await ctx.UserSessionRepository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
        ctx.AccessTokenService.DidNotReceiveWithAnyArgs().Generate(default, default!);
        ctx.SecureTokenGenerator.DidNotReceiveWithAnyArgs().Generate();
        await ctx.UnitOfWorkScope.DidNotReceiveWithAnyArgs().CommitAsync(default);
    }

    [Fact]
    public async Task ResendVerificationAsync_UnverifiedUser_IssuesNewTokenAndStartsWorkflow()
    {
        var ctx = CreateSut();
        var user = CreateUnverifiedUser(ctx.Clock);
        user.IssueEmailVerificationToken(
            TokenHash.From("old-verification-hash"),
            ctx.Clock.UtcNow.AddHours(1),
            ctx.Clock
        );
        GivenUserByEmail(ctx, user);
        ctx.SecureTokenGenerator.Generate().Returns(("verification-token", "new-verification-hash"));

        var result = await ctx.Sut.ResendVerificationAsync(" Test@Example.COM ");

        result.Should().BeOfType<AuthResult.EmailVerificationSent>();
        var freshToken = user.EmailVerificationTokens.Should().ContainSingle().Which;
        freshToken.TokenHash.Should().Be(TokenHash.From("new-verification-hash"));
        freshToken.ExpiresAt.Should().Be(ctx.Clock.UtcNow.AddHours(24));
        await ctx.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await ctx.EmailWorkflowStarter
            .Received(1)
            .StartVerificationWorkflowAsync(
                user.Id.Value,
                user.Email.Value,
                "verification-token",
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task ResendVerificationAsync_VerifiedUser_ReturnsSentButDoesNothing()
    {
        var ctx = CreateSut();
        var user = CreateVerifiedUser(ctx.Clock);
        GivenUserByEmail(ctx, user);

        var result = await ctx.Sut.ResendVerificationAsync("test@example.com");

        result.Should().BeOfType<AuthResult.EmailVerificationSent>();
        await ctx.UnitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
        await ctx.EmailWorkflowStarter
            .DidNotReceiveWithAnyArgs()
            .StartVerificationWorkflowAsync(default, default!, default!, default);
        ctx.SecureTokenGenerator.DidNotReceiveWithAnyArgs().Generate();
    }

    [Fact]
    public async Task ResendVerificationAsync_MissingUser_ReturnsSentButDoesNothing()
    {
        var ctx = CreateSut();
        GivenUserByEmail(ctx, null);

        var result = await ctx.Sut.ResendVerificationAsync("missing@example.com");

        result.Should().BeOfType<AuthResult.EmailVerificationSent>();
        await ctx.UnitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
        await ctx.EmailWorkflowStarter
            .DidNotReceiveWithAnyArgs()
            .StartVerificationWorkflowAsync(default, default!, default!, default);
        ctx.SecureTokenGenerator.DidNotReceiveWithAnyArgs().Generate();
    }

    [Fact]
    public async Task ForgotPasswordAsync_VerifiedUser_IssuesResetTokenAndStartsWorkflow()
    {
        var ctx = CreateSut();
        var user = CreateVerifiedUser(ctx.Clock);
        user.IssuePasswordResetToken(
            TokenHash.From("old-reset-hash"),
            ctx.Clock.UtcNow.AddHours(1),
            ctx.Clock
        );
        GivenUserByEmail(ctx, user);
        ctx.SecureTokenGenerator.Generate().Returns(("reset-token", "new-reset-hash"));

        var result = await ctx.Sut.ForgotPasswordAsync(" Test@Example.COM ");

        result.Should().BeOfType<AuthResult.PasswordResetSent>();
        var resetToken = user.PasswordResetTokens.Should().ContainSingle().Which;
        resetToken.TokenHash.Should().Be(TokenHash.From("new-reset-hash"));
        resetToken.ExpiresAt.Should().Be(ctx.Clock.UtcNow.AddMinutes(60));
        await ctx.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await ctx.EmailWorkflowStarter
            .Received(1)
            .StartPasswordResetWorkflowAsync(
                user.Id.Value,
                user.Email.Value,
                "reset-token",
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task ForgotPasswordAsync_UnverifiedUser_ReturnsSentButDoesNothing()
    {
        var ctx = CreateSut();
        var user = CreateUnverifiedUser(ctx.Clock);
        GivenUserByEmail(ctx, user);

        var result = await ctx.Sut.ForgotPasswordAsync("test@example.com");

        result.Should().BeOfType<AuthResult.PasswordResetSent>();
        user.PasswordResetTokens.Should().BeEmpty();
        await ctx.UnitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
        await ctx.EmailWorkflowStarter
            .DidNotReceiveWithAnyArgs()
            .StartPasswordResetWorkflowAsync(default, default!, default!, default);
        ctx.SecureTokenGenerator.DidNotReceiveWithAnyArgs().Generate();
    }

    [Fact]
    public async Task ForgotPasswordAsync_MissingUser_ReturnsSentButDoesNothing()
    {
        var ctx = CreateSut();
        GivenUserByEmail(ctx, null);

        var result = await ctx.Sut.ForgotPasswordAsync("missing@example.com");

        result.Should().BeOfType<AuthResult.PasswordResetSent>();
        await ctx.UnitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
        await ctx.EmailWorkflowStarter
            .DidNotReceiveWithAnyArgs()
            .StartPasswordResetWorkflowAsync(default, default!, default!, default);
        ctx.SecureTokenGenerator.DidNotReceiveWithAnyArgs().Generate();
    }

    [Fact]
    public async Task ResetPasswordAsync_ValidToken_UpdatesPasswordRevokesAllSessionsAndReturnsSuccess()
    {
        var ctx = CreateSut();
        var user = CreateVerifiedUser(ctx.Clock);
        var resetHash = TokenHash.FromPlain("reset-token");
        var resetToken = user.IssuePasswordResetToken(resetHash, ctx.Clock.UtcNow.AddHours(1), ctx.Clock);
        GivenUserByResetToken(ctx, user);
        ctx.PasswordHasher.Hash("new-password").Returns("new-password-hash");
        ctx.SecureTokenGenerator.Generate().Returns(("fresh-refresh-token", "fresh-refresh-hash"));

        var result = await ctx.Sut.ResetPasswordAsync("reset-token", "new-password");

        var success = result.Should().BeOfType<AuthResult.Success>().Subject;
        success.UserId.Should().Be(user.Id.Value);
        success.Email.Should().Be(user.Email.Value);
        success.AccessToken.Should().Be(TestAccessToken);
        success.RefreshToken.Should().Be("fresh-refresh-token");
        user.PasswordHash.Value.Should().Be("new-password-hash");
        resetToken.ConsumedAt.Should().Be(ctx.Clock.UtcNow);
        await ctx.UnitOfWork.Received(2).SaveChangesAsync(Arg.Any<CancellationToken>());
        await ctx.UserSessionRepository
            .Received(1)
            .RevokeAllForUserAsync(user.Id, Arg.Any<CancellationToken>());
        await ctx.UserSessionRepository
            .Received(1)
            .AddAsync(
                Arg.Is<UserSession>(session =>
                    IsIssuedSession(session, user.Id, "fresh-refresh-hash", ctx.Clock.UtcNow)
                ),
                Arg.Any<CancellationToken>()
            );
        await ctx.UnitOfWorkScope.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResetPasswordAsync_UnknownToken_ReturnsInvalidResetToken()
    {
        var ctx = CreateSut();
        GivenUserByResetToken(ctx, null);

        var result = await ctx.Sut.ResetPasswordAsync("missing-token", "new-password");

        result.Should().BeOfType<AuthResult.InvalidResetToken>();
        await ctx.UserRepository
            .Received(1)
            .GetByPasswordResetTokenHashAsync(
                TokenHash.FromPlain("missing-token"),
                Arg.Any<CancellationToken>()
            );
        ctx.PasswordHasher.DidNotReceiveWithAnyArgs().Hash(default!);
        await ctx.UnitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
        await ctx.UserSessionRepository
            .DidNotReceiveWithAnyArgs()
            .RevokeAllForUserAsync(default, default);
        await ctx.UserSessionRepository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
        ctx.AccessTokenService.DidNotReceiveWithAnyArgs().Generate(default, default!);
    }

    [Fact]
    public async Task ResetPasswordAsync_ConsumedToken_ReturnsInvalidResetToken()
    {
        var ctx = CreateSut();
        var user = CreateVerifiedUser(ctx.Clock);
        var hash = TokenHash.FromPlain("consumed-reset-token");
        user.IssuePasswordResetToken(hash, ctx.Clock.UtcNow.AddHours(1), ctx.Clock);
        user.ResetPassword(hash, PasswordHash.From("temporary-new-hash"), ctx.Clock).Should().BeTrue();
        GivenUserByResetToken(ctx, user);

        var result = await ctx.Sut.ResetPasswordAsync("consumed-reset-token", "new-password");

        result.Should().BeOfType<AuthResult.InvalidResetToken>();
        ctx.PasswordHasher.DidNotReceiveWithAnyArgs().Hash(default!);
        await ctx.UnitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
        await ctx.UserSessionRepository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
        await ctx.UserSessionRepository
            .DidNotReceiveWithAnyArgs()
            .RevokeAllForUserAsync(default, default);
        ctx.AccessTokenService.DidNotReceiveWithAnyArgs().Generate(default, default!);
    }

    [Fact]
    public async Task ResetPasswordAsync_ExpiredToken_ReturnsResetTokenExpired()
    {
        var ctx = CreateSut();
        var user = CreateVerifiedUser(ctx.Clock);
        var hash = TokenHash.FromPlain("expired-reset-token");
        var token = user.IssuePasswordResetToken(hash, ctx.Clock.UtcNow.AddMinutes(-1), ctx.Clock);
        GivenUserByResetToken(ctx, user);

        var result = await ctx.Sut.ResetPasswordAsync("expired-reset-token", "new-password");

        result.Should().BeOfType<AuthResult.ResetTokenExpired>();
        token.ConsumedAt.Should().BeNull();
        ctx.PasswordHasher.DidNotReceiveWithAnyArgs().Hash(default!);
        await ctx.UnitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
        await ctx.UserSessionRepository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
        await ctx.UserSessionRepository
            .DidNotReceiveWithAnyArgs()
            .RevokeAllForUserAsync(default, default);
        ctx.AccessTokenService.DidNotReceiveWithAnyArgs().Generate(default, default!);
    }

    [Fact]
    public async Task ResetPasswordAsync_SaveChangesThrowsConcurrencyConflict_ReturnsInvalid()
    {
        var ctx = CreateSut();
        var user = CreateVerifiedUser(ctx.Clock);
        var hash = TokenHash.FromPlain("race-reset-token");
        user.IssuePasswordResetToken(hash, ctx.Clock.UtcNow.AddHours(1), ctx.Clock);
        GivenUserByResetToken(ctx, user);
        ctx.PasswordHasher.Hash("new-password").Returns("new-password-hash");
        ctx.UnitOfWork
            .When(x => x.SaveChangesAsync(Arg.Any<CancellationToken>()))
            .Do(_ => throw new ConcurrencyConflictException("test"));

        var result = await ctx.Sut.ResetPasswordAsync("race-reset-token", "new-password");

        result.Should().BeOfType<AuthResult.InvalidResetToken>();
        user.PasswordHash.Value.Should().Be("new-password-hash");
        user.PasswordResetTokens.Single().ConsumedAt.Should().Be(ctx.Clock.UtcNow);
        await ctx.UserSessionRepository
            .DidNotReceiveWithAnyArgs()
            .RevokeAllForUserAsync(default, default);
        await ctx.UserSessionRepository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
        ctx.AccessTokenService.DidNotReceiveWithAnyArgs().Generate(default, default!);
        ctx.SecureTokenGenerator.DidNotReceiveWithAnyArgs().Generate();
        await ctx.UnitOfWorkScope.DidNotReceiveWithAnyArgs().CommitAsync(default);
    }

    private static SutContext CreateSut(FakeClock? clock = null)
    {
        clock ??= new FakeClock(TestNow);
        var userRepository = Substitute.For<IUserRepository>();
        var userSessionRepository = Substitute.For<IUserSessionRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var unitOfWorkScope = Substitute.For<IUnitOfWorkScope>();
        unitOfWork
            .BeginAsync(default)
            .ReturnsForAnyArgs(
                (Func<CallInfo, Task<IUnitOfWorkScope>>)(_ => Task.FromResult(unitOfWorkScope))
            );
        var passwordHasher = Substitute.For<IPasswordHasher>();
        var accessTokenService = Substitute.For<IAccessTokenService>();
        var secureTokenGenerator = Substitute.For<ISecureTokenGenerator>();
        var totpService = Substitute.For<ITotpService>();
        var totpSecretProtector = Substitute.For<ITotpSecretProtector>();
        var emailWorkflowStarter = Substitute.For<IEmailWorkflowStarter>();

        userRepository
            .GetByIdAsync(default, default)
            .ReturnsForAnyArgs(
                (Func<CallInfo, Task<User?>>)(_ => Task.FromResult<User?>(null))
            );
        userRepository
            .GetByEmailAsync(default!, default)
            .ReturnsForAnyArgs(
                (Func<CallInfo, Task<User?>>)(_ => Task.FromResult<User?>(null))
            );
        userRepository
            .GetByEmailVerificationTokenHashAsync(default!, default)
            .ReturnsForAnyArgs(
                (Func<CallInfo, Task<User?>>)(_ => Task.FromResult<User?>(null))
            );
        userRepository
            .GetByPasswordResetTokenHashAsync(default!, default)
            .ReturnsForAnyArgs(
                (Func<CallInfo, Task<User?>>)(_ => Task.FromResult<User?>(null))
            );
        userRepository
            .GetByTwoFactorChallengeHashAsync(default!, default)
            .ReturnsForAnyArgs(
                (Func<CallInfo, Task<User?>>)(_ => Task.FromResult<User?>(null))
            );
        userRepository
            .AddAsync(default!, default)
            .ReturnsForAnyArgs((Func<CallInfo, Task>)(_ => Task.CompletedTask));
        userSessionRepository
            .GetByTokenHashAsync(default!, default)
            .ReturnsForAnyArgs(
                (Func<CallInfo, Task<UserSession?>>)(
                    _ => Task.FromResult<UserSession?>(null)
                )
            );
        userSessionRepository
            .GetActiveByUserIdAsync(default, default)
            .ReturnsForAnyArgs(
                (Func<CallInfo, Task<IReadOnlyList<UserSession>>>)(
                    _ => Task.FromResult<IReadOnlyList<UserSession>>(
                        Array.Empty<UserSession>()
                    )
                )
            );
        userSessionRepository
            .AddAsync(default!, default)
            .ReturnsForAnyArgs((Func<CallInfo, Task>)(_ => Task.CompletedTask));
        userSessionRepository
            .RevokeAllForUserAsync(default, default)
            .ReturnsForAnyArgs((Func<CallInfo, Task>)(_ => Task.CompletedTask));
        unitOfWork.SaveChangesAsync(default).ReturnsForAnyArgs(Task.CompletedTask);

        passwordHasher.Hash(default!).ReturnsForAnyArgs("hashed-password");
        passwordHasher.Verify(default!, default!).ReturnsForAnyArgs(false);
        passwordHasher.DummyHash.Returns("dummy-hash");

        accessTokenService.Generate(default, default!).ReturnsForAnyArgs(TestAccessToken);
        secureTokenGenerator.Generate().Returns(("generated-token", "generated-token-hash"));
        totpService
            .Verify(default!, default!)
            .ReturnsForAnyArgs(new TotpVerificationResult.NoMatch());
        totpSecretProtector.Unprotect(default!).ReturnsForAnyArgs([]);

        emailWorkflowStarter
            .StartVerificationWorkflowAsync(default, default!, default!, default)
            .ReturnsForAnyArgs((Func<CallInfo, Task>)(_ => Task.CompletedTask));
        emailWorkflowStarter
            .StartPasswordResetWorkflowAsync(default, default!, default!, default)
            .ReturnsForAnyArgs((Func<CallInfo, Task>)(_ => Task.CompletedTask));

        var sut = new AuthService(
            userRepository,
            userSessionRepository,
            unitOfWork,
            passwordHasher,
            accessTokenService,
            secureTokenGenerator,
            totpService,
            totpSecretProtector,
            emailWorkflowStarter,
            clock
        );

        return new SutContext(
            sut,
            userRepository,
            userSessionRepository,
            unitOfWork,
            unitOfWorkScope,
            passwordHasher,
            accessTokenService,
            secureTokenGenerator,
            totpService,
            totpSecretProtector,
            emailWorkflowStarter,
            clock
        );
    }

    private static User CreateVerifiedUser(IClock clock)
    {
        var user = CreateUnverifiedUser(clock);
        var hash = TokenHash.From("verified-email-token-hash");
        user.IssueEmailVerificationToken(hash, clock.UtcNow.AddHours(1), clock);
        user.VerifyEmail(hash, clock).Should().BeTrue();
        return user;
    }

    private static User CreateTwoFactorEnabledUser(
        FakeClock clock,
        long lastUsedStep = 100,
        IReadOnlyList<string>? recoveryCodeHashes = null
    )
    {
        var user = CreateVerifiedUser(clock);
        user.StartTwoFactorSetup(EncryptedTotpSecret.From("protected-secret"), clock);
        user.ConfirmTwoFactor(
                lastUsedStep,
                (recoveryCodeHashes ?? ["recovery-code-hash"])
                    .Select(RecoveryCodeHash.From)
                    .ToArray(),
                clock
            )
            .Should()
            .BeTrue();
        return user;
    }

    private static User CreateUnverifiedUser(IClock clock) =>
        User.Register(
            Email.Normalize("test@example.com"),
            PasswordHash.From("hashed-password"),
            clock
        );

    private static UserSession CreateSession(
        UserId userId,
        string plainRefreshToken,
        IClock clock,
        DateTimeOffset? expiresAt = null
    ) =>
        UserSession.Issue(
            userId,
            TokenHash.FromPlain(plainRefreshToken),
            expiresAt ?? clock.UtcNow.AddDays(1),
            clock
        );

    private static void GivenUserByEmail(SutContext ctx, User? user) =>
        ctx.UserRepository
            .GetByEmailAsync(default!, default)
            .ReturnsForAnyArgs((Func<CallInfo, Task<User?>>)(_ => Task.FromResult(user)));

    private static void GivenUserById(SutContext ctx, User? user) =>
        ctx.UserRepository
            .GetByIdAsync(default, default)
            .ReturnsForAnyArgs((Func<CallInfo, Task<User?>>)(_ => Task.FromResult(user)));

    private static void GivenUserByVerificationToken(SutContext ctx, User? user) =>
        ctx.UserRepository
            .GetByEmailVerificationTokenHashAsync(default!, default)
            .ReturnsForAnyArgs((Func<CallInfo, Task<User?>>)(_ => Task.FromResult(user)));

    private static void GivenUserByResetToken(SutContext ctx, User? user) =>
        ctx.UserRepository
            .GetByPasswordResetTokenHashAsync(default!, default)
            .ReturnsForAnyArgs((Func<CallInfo, Task<User?>>)(_ => Task.FromResult(user)));

    private static void GivenUserByTwoFactorChallenge(SutContext ctx, User? user) =>
        ctx.UserRepository
            .GetByTwoFactorChallengeHashAsync(default!, default)
            .ReturnsForAnyArgs((Func<CallInfo, Task<User?>>)(_ => Task.FromResult(user)));

    private static void GivenSessionByRefreshToken(SutContext ctx, UserSession? session) =>
        ctx.UserSessionRepository
            .GetByTokenHashAsync(default!, default)
            .ReturnsForAnyArgs(
                (Func<CallInfo, Task<UserSession?>>)(_ => Task.FromResult(session))
            );

    private static byte[] GivenTotpSecret(SutContext ctx)
    {
        var secret = new byte[] { 1, 2, 3, 4 };
        ctx.TotpSecretProtector.Unprotect("protected-secret").Returns(secret);
        return secret;
    }

    private static TokenHash IssueTwoFactorChallenge(
        User user,
        string plainToken,
        FakeClock clock,
        TimeSpan expiresIn
    )
    {
        var hash = TokenHash.FromPlain(plainToken);
        user.IssueTwoFactorChallenge(hash, clock.UtcNow.Add(expiresIn), clock);
        return hash;
    }

    private static bool IsIssuedSession(
        UserSession? session,
        UserId userId,
        string hashedToken,
        DateTimeOffset now
    ) =>
        session is not null
        && session.UserId == userId
        && session.TokenHash == TokenHash.From(hashedToken)
        && session.CreatedAt == now
        && session.ExpiresAt == now.AddDays(7)
        && session.IsActive(now);

    private sealed record SutContext(
        AuthService Sut,
        IUserRepository UserRepository,
        IUserSessionRepository UserSessionRepository,
        IUnitOfWork UnitOfWork,
        IUnitOfWorkScope UnitOfWorkScope,
        IPasswordHasher PasswordHasher,
        IAccessTokenService AccessTokenService,
        ISecureTokenGenerator SecureTokenGenerator,
        ITotpService TotpService,
        ITotpSecretProtector TotpSecretProtector,
        IEmailWorkflowStarter EmailWorkflowStarter,
        FakeClock Clock
    );
}
