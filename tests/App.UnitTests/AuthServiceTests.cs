using App.Identity;
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
        await ctx.UserRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
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
        await ctx.UserRepository.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
        await ctx.EmailWorkflowStarter
            .DidNotReceiveWithAnyArgs()
            .StartVerificationWorkflowAsync(default, default!, default!, default);
    }

    [Fact]
    public async Task LoginAsync_ValidVerifiedUser_ReturnsSuccessAndIssuesSession()
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
        await ctx.UserSessionRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
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
        await ctx.UserSessionRepository.Received(2).SaveChangesAsync(Arg.Any<CancellationToken>());
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
                TokenHash.From(TokenHasher.Hash("missing-refresh-token")),
                Arg.Any<CancellationToken>()
            );
        await ctx.UserSessionRepository
            .DidNotReceiveWithAnyArgs()
            .RevokeAllForUserAsync(default, default);
        await ctx.UserSessionRepository.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
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
        await ctx.UserSessionRepository.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
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
        await ctx.UserSessionRepository.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
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
        ctx.UserSessionRepository
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
        await ctx.UserSessionRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
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
        var verificationHash = TokenHash.From(TokenHasher.Hash("verification-token"));
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
        await ctx.UserRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
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
                TokenHash.From(TokenHasher.Hash("missing-token")),
                Arg.Any<CancellationToken>()
            );
        await ctx.UserRepository.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
        await ctx.UserSessionRepository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
        ctx.AccessTokenService.DidNotReceiveWithAnyArgs().Generate(default, default!);
    }

    [Fact]
    public async Task VerifyEmailAsync_ConsumedToken_ReturnsInvalidVerificationToken()
    {
        var ctx = CreateSut();
        var user = CreateUnverifiedUser(ctx.Clock);
        var hash = TokenHash.From(TokenHasher.Hash("consumed-token"));
        var token = user.IssueEmailVerificationToken(hash, ctx.Clock.UtcNow.AddHours(1), ctx.Clock);
        user.VerifyEmail(hash, ctx.Clock).Should().BeTrue();
        GivenUserByVerificationToken(ctx, user);

        var result = await ctx.Sut.VerifyEmailAsync("consumed-token");

        result.Should().BeOfType<AuthResult.InvalidVerificationToken>();
        token.IsConsumed.Should().BeTrue();
        await ctx.UserRepository.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
        await ctx.UserSessionRepository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
        ctx.AccessTokenService.DidNotReceiveWithAnyArgs().Generate(default, default!);
        ctx.SecureTokenGenerator.DidNotReceiveWithAnyArgs().Generate();
    }

    [Fact]
    public async Task VerifyEmailAsync_ExpiredToken_ReturnsVerificationTokenExpired()
    {
        var ctx = CreateSut();
        var user = CreateUnverifiedUser(ctx.Clock);
        var hash = TokenHash.From(TokenHasher.Hash("expired-token"));
        var token = user.IssueEmailVerificationToken(hash, ctx.Clock.UtcNow.AddMinutes(-1), ctx.Clock);
        GivenUserByVerificationToken(ctx, user);

        var result = await ctx.Sut.VerifyEmailAsync("expired-token");

        result.Should().BeOfType<AuthResult.VerificationTokenExpired>();
        user.EmailVerified.Should().BeFalse();
        token.ConsumedAt.Should().BeNull();
        await ctx.UserRepository.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
        await ctx.UserSessionRepository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
        ctx.AccessTokenService.DidNotReceiveWithAnyArgs().Generate(default, default!);
        ctx.SecureTokenGenerator.DidNotReceiveWithAnyArgs().Generate();
    }

    [Fact]
    public async Task VerifyEmailAsync_SaveChangesThrowsConcurrencyConflict_ReturnsInvalid()
    {
        var ctx = CreateSut();
        var user = CreateUnverifiedUser(ctx.Clock);
        var hash = TokenHash.From(TokenHasher.Hash("race-token"));
        user.IssueEmailVerificationToken(hash, ctx.Clock.UtcNow.AddHours(1), ctx.Clock);
        GivenUserByVerificationToken(ctx, user);
        ctx.UserRepository
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
        await ctx.UserRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
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
        await ctx.UserRepository.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
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
        await ctx.UserRepository.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
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
        await ctx.UserRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
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
        await ctx.UserRepository.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
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
        await ctx.UserRepository.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
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
        var resetHash = TokenHash.From(TokenHasher.Hash("reset-token"));
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
        await ctx.UserRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
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
                TokenHash.From(TokenHasher.Hash("missing-token")),
                Arg.Any<CancellationToken>()
            );
        ctx.PasswordHasher.DidNotReceiveWithAnyArgs().Hash(default!);
        await ctx.UserRepository.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
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
        var hash = TokenHash.From(TokenHasher.Hash("consumed-reset-token"));
        user.IssuePasswordResetToken(hash, ctx.Clock.UtcNow.AddHours(1), ctx.Clock);
        user.ResetPassword(hash, PasswordHash.From("temporary-new-hash"), ctx.Clock).Should().BeTrue();
        GivenUserByResetToken(ctx, user);

        var result = await ctx.Sut.ResetPasswordAsync("consumed-reset-token", "new-password");

        result.Should().BeOfType<AuthResult.InvalidResetToken>();
        ctx.PasswordHasher.DidNotReceiveWithAnyArgs().Hash(default!);
        await ctx.UserRepository.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
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
        var hash = TokenHash.From(TokenHasher.Hash("expired-reset-token"));
        var token = user.IssuePasswordResetToken(hash, ctx.Clock.UtcNow.AddMinutes(-1), ctx.Clock);
        GivenUserByResetToken(ctx, user);

        var result = await ctx.Sut.ResetPasswordAsync("expired-reset-token", "new-password");

        result.Should().BeOfType<AuthResult.ResetTokenExpired>();
        token.ConsumedAt.Should().BeNull();
        ctx.PasswordHasher.DidNotReceiveWithAnyArgs().Hash(default!);
        await ctx.UserRepository.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
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
        var hash = TokenHash.From(TokenHasher.Hash("race-reset-token"));
        user.IssuePasswordResetToken(hash, ctx.Clock.UtcNow.AddHours(1), ctx.Clock);
        GivenUserByResetToken(ctx, user);
        ctx.PasswordHasher.Hash("new-password").Returns("new-password-hash");
        ctx.UserRepository
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
            .AddAsync(default!, default)
            .ReturnsForAnyArgs((Func<CallInfo, Task>)(_ => Task.CompletedTask));
        userRepository
            .SaveChangesAsync(default)
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
        userSessionRepository
            .SaveChangesAsync(default)
            .ReturnsForAnyArgs((Func<CallInfo, Task>)(_ => Task.CompletedTask));

        passwordHasher.Hash(default!).ReturnsForAnyArgs("hashed-password");
        passwordHasher.Verify(default!, default!).ReturnsForAnyArgs(false);
        passwordHasher.DummyHash.Returns("dummy-hash");

        accessTokenService.Generate(default, default!).ReturnsForAnyArgs(TestAccessToken);
        secureTokenGenerator.Generate().Returns(("generated-token", "generated-token-hash"));

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
            TokenHash.From(TokenHasher.Hash(plainRefreshToken)),
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

    private static void GivenSessionByRefreshToken(SutContext ctx, UserSession? session) =>
        ctx.UserSessionRepository
            .GetByTokenHashAsync(default!, default)
            .ReturnsForAnyArgs(
                (Func<CallInfo, Task<UserSession?>>)(_ => Task.FromResult(session))
            );

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
        IEmailWorkflowStarter EmailWorkflowStarter,
        FakeClock Clock
    );
}
