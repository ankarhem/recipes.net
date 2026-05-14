using App.Auth;
using AwesomeAssertions;
using NSubstitute;
using NSubstitute.Core;
using Xunit;
using DomainEmailVerificationToken = Domain.User.EmailVerificationToken;
using DomainPasswordResetToken = Domain.User.PasswordResetToken;
using DomainRefreshToken = Domain.User.RefreshToken;
using DomainUser = Domain.User.User;

namespace App.UnitTests;

public class AuthServiceTests
{
    private static readonly AccessToken TestToken = new()
    {
        Token = "token",
        ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15),
    };

    [Fact]
    public async Task RegisterAsync_NewEmail_ReturnsRegistrationPendingAndSendsVerification()
    {
        var ctx = CreateSut();
        var user = CreateUser(email: "test@example.com", passwordHash: "hashed-password");

        ctx.UserRepository
            .GetByEmailAsync(default!, default)
            .ReturnsForAnyArgs(
                (Func<CallInfo, Task<DomainUser?>>)(_ => Task.FromResult<DomainUser?>(null))
            );
        ctx.PasswordHasher.Hash("password").Returns("hashed-password");
        ctx.UserRepository
            .CreateAsync(default!, default!, default)
            .ReturnsForAnyArgs((Func<CallInfo, Task<DomainUser>>)(_ => Task.FromResult(user)));
        ctx.SecureTokenGenerator.Generate().Returns(("verification-token", "hashed-verification-token"));

        var result = await ctx.Sut.RegisterAsync(" Test@Example.COM ", "password");

        var pending = result.Should().BeOfType<AuthResult.RegistrationPending>().Subject;
        pending.UserId.Should().Be(user.Id);
        pending.Email.Should().Be("test@example.com");
        await ctx.UserRepository
            .Received(1)
            .GetByEmailAsync("test@example.com", Arg.Any<CancellationToken>());
        await ctx.UserRepository
            .Received(1)
            .CreateAsync("test@example.com", "hashed-password", Arg.Any<CancellationToken>());
        ctx.SecureTokenGenerator.Received(1).Generate();
        await ctx.EmailVerificationTokenRepository
            .Received(1)
            .StoreAsync(
                user.Id,
                "hashed-verification-token",
                Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>()
            );
        await ctx.EmailWorkflowStarter
            .Received(1)
            .StartVerificationWorkflowAsync(
                user.Id,
                "test@example.com",
                "verification-token",
                Arg.Any<CancellationToken>()
            );
        ctx.AccessTokenService.DidNotReceiveWithAnyArgs().Generate(default, default!);
        await ctx.RefreshTokenRepository.DidNotReceiveWithAnyArgs().StoreAsync(default, default!, default, default);
    }

    [Fact]
    public async Task RegisterAsync_DuplicateEmail_ReturnsEmailAlreadyRegistered()
    {
        var ctx = CreateSut();
        var existingUser = CreateUser(email: "test@example.com", passwordHash: "hashed-password");

        ctx.UserRepository
            .GetByEmailAsync(default!, default)
            .ReturnsForAnyArgs(
                (Func<CallInfo, Task<DomainUser?>>)(_ => Task.FromResult<DomainUser?>(existingUser))
            );

        var result = await ctx.Sut.RegisterAsync("test@example.com", "password");

        result.Should().BeOfType<AuthResult.EmailAlreadyRegistered>();
        ctx.PasswordHasher.DidNotReceiveWithAnyArgs().Hash(default!);
        await ctx.UserRepository.DidNotReceiveWithAnyArgs().CreateAsync(default!, default!, default);
        ctx.AccessTokenService.DidNotReceiveWithAnyArgs().Generate(default, default!);
        ctx.SecureTokenGenerator.DidNotReceiveWithAnyArgs().Generate();
        await ctx.RefreshTokenRepository
            .DidNotReceiveWithAnyArgs()
            .StoreAsync(default, default!, default, default);
    }

    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsSuccessAndStoresRefreshToken()
    {
        var ctx = CreateSut();
        var user = CreateUser(email: "test@example.com", passwordHash: "hashed-password");

        ctx.UserRepository
            .GetByEmailAsync(default!, default)
            .ReturnsForAnyArgs(
                (Func<CallInfo, Task<DomainUser?>>)(_ => Task.FromResult<DomainUser?>(user))
            );
        ctx.PasswordHasher.Verify("password", user.PasswordHash).Returns(true);
        ctx.AccessTokenService.Generate(user.Id, user.Email).Returns(TestToken);
        ctx.SecureTokenGenerator.Generate().Returns(("refresh-token", "hashed-refresh-token"));

        var before = DateTimeOffset.UtcNow;
        var result = await ctx.Sut.LoginAsync(" Test@Example.COM ", "password");
        var after = DateTimeOffset.UtcNow;

        var success = result.Should().BeOfType<AuthResult.Success>().Subject;
        success.UserId.Should().Be(user.Id);
        success.Email.Should().Be("test@example.com");
        success.AccessToken.Should().Be(TestToken);
        success.RefreshToken.Should().Be("refresh-token");
        await ctx.UserRepository
            .Received(1)
            .GetByEmailAsync("test@example.com", Arg.Any<CancellationToken>());
        ctx.PasswordHasher.Received(1).Verify("password", "hashed-password");
        ctx.SecureTokenGenerator.Received(1).Generate();
        await ctx.RefreshTokenRepository
            .Received(1)
            .StoreAsync(
                user.Id,
                "hashed-refresh-token",
                Arg.Is<DateTimeOffset>(expiresAt => IsSevenDayExpiry(expiresAt, before, after)),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task LoginAsync_UnknownEmail_ReturnsInvalidCredentials()
    {
        var ctx = CreateSut();

        ctx.UserRepository
            .GetByEmailAsync(default!, default)
            .ReturnsForAnyArgs(
                (Func<CallInfo, Task<DomainUser?>>)(_ => Task.FromResult<DomainUser?>(null))
            );

        var result = await ctx.Sut.LoginAsync("missing@example.com", "password");

        result.Should().BeOfType<AuthResult.InvalidCredentials>();
        ctx.AccessTokenService.DidNotReceiveWithAnyArgs().Generate(default, default!);
        ctx.SecureTokenGenerator.DidNotReceiveWithAnyArgs().Generate();
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_ReturnsInvalidCredentials()
    {
        var ctx = CreateSut();
        var user = CreateUser(email: "test@example.com", passwordHash: "hashed-password");

        ctx.UserRepository
            .GetByEmailAsync(default!, default)
            .ReturnsForAnyArgs(
                (Func<CallInfo, Task<DomainUser?>>)(_ => Task.FromResult<DomainUser?>(user))
            );
        ctx.PasswordHasher.Verify("wrong-password", user.PasswordHash).Returns(false);

        var result = await ctx.Sut.LoginAsync("test@example.com", "wrong-password");

        result.Should().BeOfType<AuthResult.InvalidCredentials>();
        ctx.PasswordHasher.Received(1).Verify("wrong-password", "hashed-password");
        ctx.AccessTokenService.DidNotReceiveWithAnyArgs().Generate(default, default!);
        ctx.SecureTokenGenerator.DidNotReceiveWithAnyArgs().Generate();
    }

    [Fact]
    public async Task LoginAsync_UnverifiedUser_ReturnsEmailNotVerified()
    {
        var ctx = CreateSut();
        var user = CreateUser(
            email: "test@example.com",
            passwordHash: "hashed-password",
            emailVerified: false
        );

        ctx.UserRepository
            .GetByEmailAsync(default!, default)
            .ReturnsForAnyArgs(
                (Func<CallInfo, Task<DomainUser?>>)(_ => Task.FromResult<DomainUser?>(user))
            );
        ctx.PasswordHasher.Verify("password", user.PasswordHash).Returns(true);

        var result = await ctx.Sut.LoginAsync("test@example.com", "password");

        result.Should().BeOfType<AuthResult.EmailNotVerified>();
        ctx.PasswordHasher.Received(1).Verify("password", "hashed-password");
        ctx.AccessTokenService.DidNotReceiveWithAnyArgs().Generate(default, default!);
        ctx.SecureTokenGenerator.DidNotReceiveWithAnyArgs().Generate();
        await ctx.RefreshTokenRepository
            .DidNotReceiveWithAnyArgs()
            .StoreAsync(default, default!, default, default);
    }

    [Fact]
    public async Task RefreshAsync_ValidToken_ReturnsSuccessAndRotatesRefreshToken()
    {
        var ctx = CreateSut();
        var user = CreateUser(email: "test@example.com", passwordHash: "hashed-password");
        var refreshToken = CreateRefreshToken(user.Id, TokenHasher.Hash("refresh-token"));

        ctx.RefreshTokenRepository
            .FindByTokenHashAsync(default!, default)
            .ReturnsForAnyArgs(
                (Func<CallInfo, Task<DomainRefreshToken?>>)(
                    _ => Task.FromResult<DomainRefreshToken?>(refreshToken)
                )
            );
        ctx.UserRepository
            .GetByIdAsync(default, default)
            .ReturnsForAnyArgs(
                (Func<CallInfo, Task<DomainUser?>>)(_ => Task.FromResult<DomainUser?>(user))
            );
        ctx.AccessTokenService.Generate(user.Id, user.Email).Returns(TestToken);
        ctx.SecureTokenGenerator.Generate().Returns(("new-refresh-token", "new-hashed-token"));

        var before = DateTimeOffset.UtcNow;
        var result = await ctx.Sut.RefreshAsync("refresh-token");
        var after = DateTimeOffset.UtcNow;

        var success = result.Should().BeOfType<AuthResult.Success>().Subject;
        success.UserId.Should().Be(user.Id);
        success.Email.Should().Be(user.Email);
        success.AccessToken.Should().Be(TestToken);
        success.RefreshToken.Should().Be("new-refresh-token");
        await ctx.RefreshTokenRepository
            .Received(1)
            .FindByTokenHashAsync(TokenHasher.Hash("refresh-token"), Arg.Any<CancellationToken>());
        await ctx.RefreshTokenRepository
            .Received(1)
            .TryRevokeAsync(refreshToken.Id, Arg.Any<CancellationToken>());
        await ctx.RefreshTokenRepository
            .Received(1)
            .StoreAsync(
                user.Id,
                "new-hashed-token",
                Arg.Is<DateTimeOffset>(expiresAt => IsSevenDayExpiry(expiresAt, before, after)),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task RefreshAsync_UnverifiedUser_ReturnsEmailNotVerifiedAndRevokesAllUserTokens()
    {
        var ctx = CreateSut();
        var user = CreateUser(
            email: "test@example.com",
            passwordHash: "hashed-password",
            emailVerified: false
        );
        var refreshToken = CreateRefreshToken(user.Id, TokenHasher.Hash("refresh-token"));

        ctx.RefreshTokenRepository
            .FindByTokenHashAsync(default!, default)
            .ReturnsForAnyArgs(
                (Func<CallInfo, Task<DomainRefreshToken?>>)(
                    _ => Task.FromResult<DomainRefreshToken?>(refreshToken)
                )
            );
        ctx.UserRepository
            .GetByIdAsync(default, default)
            .ReturnsForAnyArgs(
                (Func<CallInfo, Task<DomainUser?>>)(_ => Task.FromResult<DomainUser?>(user))
            );

        var result = await ctx.Sut.RefreshAsync("refresh-token");

        result.Should().BeOfType<AuthResult.EmailNotVerified>();
        await ctx.RefreshTokenRepository
            .Received(1)
            .FindByTokenHashAsync(TokenHasher.Hash("refresh-token"), Arg.Any<CancellationToken>());
        await ctx.RefreshTokenRepository
            .Received(1)
            .TryRevokeAsync(refreshToken.Id, Arg.Any<CancellationToken>());
        await ctx.RefreshTokenRepository
            .Received(1)
            .RevokeAllForUserAsync(user.Id, Arg.Any<CancellationToken>());
        ctx.AccessTokenService.DidNotReceiveWithAnyArgs().Generate(default, default!);
        ctx.SecureTokenGenerator.DidNotReceiveWithAnyArgs().Generate();
        await ctx.RefreshTokenRepository
            .DidNotReceiveWithAnyArgs()
            .StoreAsync(default, default!, default, default);
    }

    [Fact]
    public async Task RefreshAsync_InvalidToken_ReturnsInvalidRefreshToken()
    {
        var ctx = CreateSut();

        ctx.RefreshTokenRepository
            .FindByTokenHashAsync(default!, default)
            .ReturnsForAnyArgs(
                (Func<CallInfo, Task<DomainRefreshToken?>>)(
                    _ => Task.FromResult<DomainRefreshToken?>(null)
                )
            );

        var result = await ctx.Sut.RefreshAsync("missing-refresh-token");

        result.Should().BeOfType<AuthResult.InvalidRefreshToken>();
        await ctx.RefreshTokenRepository
            .Received(1)
            .FindByTokenHashAsync(TokenHasher.Hash("missing-refresh-token"), Arg.Any<CancellationToken>());
        await ctx.RefreshTokenRepository
            .DidNotReceiveWithAnyArgs()
            .RevokeAllForUserAsync(default, default);
        await ctx.RefreshTokenRepository.DidNotReceiveWithAnyArgs().TryRevokeAsync(default, default);
        await ctx.RefreshTokenRepository
            .DidNotReceiveWithAnyArgs()
            .StoreAsync(default, default!, default, default);
    }

    [Fact]
    public async Task RefreshAsync_ExpiredToken_ReturnsInvalidRefreshTokenAndRevokesAllUserTokens()
    {
        var ctx = CreateSut();
        var userId = Guid.NewGuid();
        var refreshToken = CreateRefreshToken(
            userId,
            TokenHasher.Hash("expired-refresh-token"),
            expiresAt: DateTimeOffset.UtcNow.AddMinutes(-1)
        );

        ctx.RefreshTokenRepository
            .FindByTokenHashAsync(default!, default)
            .ReturnsForAnyArgs(
                (Func<CallInfo, Task<DomainRefreshToken?>>)(
                    _ => Task.FromResult<DomainRefreshToken?>(refreshToken)
                )
            );

        var result = await ctx.Sut.RefreshAsync("expired-refresh-token");

        result.Should().BeOfType<AuthResult.InvalidRefreshToken>();
        await ctx.RefreshTokenRepository
            .Received(1)
            .RevokeAllForUserAsync(userId, Arg.Any<CancellationToken>());
        await ctx.RefreshTokenRepository.DidNotReceiveWithAnyArgs().TryRevokeAsync(default, default);
        ctx.AccessTokenService.DidNotReceiveWithAnyArgs().Generate(default, default!);
        ctx.SecureTokenGenerator.DidNotReceiveWithAnyArgs().Generate();
    }

    [Fact]
    public async Task RefreshAsync_RevokedToken_ReturnsInvalidRefreshTokenAndRevokesAllUserTokens()
    {
        var ctx = CreateSut();
        var userId = Guid.NewGuid();
        var refreshToken = CreateRefreshToken(
            userId,
            TokenHasher.Hash("revoked-refresh-token"),
            revokedAt: DateTimeOffset.UtcNow.AddMinutes(-1)
        );

        ctx.RefreshTokenRepository
            .FindByTokenHashAsync(default!, default)
            .ReturnsForAnyArgs(
                (Func<CallInfo, Task<DomainRefreshToken?>>)(
                    _ => Task.FromResult<DomainRefreshToken?>(refreshToken)
                )
            );

        var result = await ctx.Sut.RefreshAsync("revoked-refresh-token");

        result.Should().BeOfType<AuthResult.InvalidRefreshToken>();
        await ctx.RefreshTokenRepository
            .Received(1)
            .RevokeAllForUserAsync(userId, Arg.Any<CancellationToken>());
        await ctx.RefreshTokenRepository.DidNotReceiveWithAnyArgs().TryRevokeAsync(default, default);
        ctx.AccessTokenService.DidNotReceiveWithAnyArgs().Generate(default, default!);
        ctx.SecureTokenGenerator.DidNotReceiveWithAnyArgs().Generate();
    }

    [Fact]
    public async Task RefreshAsync_TokenLosesRevokeRace_RevokesAllUserTokensAndReturnsInvalid()
    {
        var ctx = CreateSut();
        var user = CreateUser(email: "test@example.com", passwordHash: "hashed-password");
        var refreshToken = CreateRefreshToken(user.Id, TokenHasher.Hash("refresh-token"));

        ctx.RefreshTokenRepository
            .FindByTokenHashAsync(default!, default)
            .ReturnsForAnyArgs(
                (Func<CallInfo, Task<DomainRefreshToken?>>)(
                    _ => Task.FromResult<DomainRefreshToken?>(refreshToken)
                )
            );
        ctx.RefreshTokenRepository
            .TryRevokeAsync(default, default)
            .ReturnsForAnyArgs((Func<CallInfo, Task<bool>>)(_ => Task.FromResult(false)));

        var result = await ctx.Sut.RefreshAsync("refresh-token");

        result.Should().BeOfType<AuthResult.InvalidRefreshToken>();
        await ctx.RefreshTokenRepository
            .Received(1)
            .TryRevokeAsync(refreshToken.Id, Arg.Any<CancellationToken>());
        await ctx.RefreshTokenRepository
            .Received(1)
            .RevokeAllForUserAsync(user.Id, Arg.Any<CancellationToken>());
        await ctx.UserRepository.DidNotReceiveWithAnyArgs().GetByIdAsync(default, default);
        ctx.AccessTokenService.DidNotReceiveWithAnyArgs().Generate(default, default!);
        ctx.SecureTokenGenerator.DidNotReceiveWithAnyArgs().Generate();
        await ctx.RefreshTokenRepository
            .DidNotReceiveWithAnyArgs()
            .StoreAsync(default, default!, default, default);
    }

    [Fact]
    public async Task VerifyEmailAsync_ValidToken_ReturnsSuccessAndMarksVerified()
    {
        var ctx = CreateSut();
        var user = CreateUser(email: "test@example.com", passwordHash: "hashed-password");
        var stored = CreateEmailVerificationToken(user.Id, TokenHasher.Hash("verification-token"));

        ctx.EmailVerificationTokenRepository
            .FindByTokenHashAsync(default!, default)
            .ReturnsForAnyArgs(
                (Func<CallInfo, Task<DomainEmailVerificationToken?>>)(
                    _ => Task.FromResult<DomainEmailVerificationToken?>(stored)
                )
            );
        ctx.UserRepository
            .GetByIdAsync(default, default)
            .ReturnsForAnyArgs(
                (Func<CallInfo, Task<DomainUser?>>)(_ => Task.FromResult<DomainUser?>(user))
            );
        ctx.AccessTokenService.Generate(user.Id, user.Email).Returns(TestToken);
        ctx.SecureTokenGenerator.Generate().Returns(("refresh-token", "hashed-refresh-token"));

        var result = await ctx.Sut.VerifyEmailAsync("verification-token");

        var success = result.Should().BeOfType<AuthResult.Success>().Subject;
        success.UserId.Should().Be(user.Id);
        success.Email.Should().Be(user.Email);
        success.AccessToken.Should().Be(TestToken);
        success.RefreshToken.Should().Be("refresh-token");
        await ctx.EmailVerificationTokenRepository
            .Received(1)
            .FindByTokenHashAsync(TokenHasher.Hash("verification-token"), Arg.Any<CancellationToken>());
        await ctx.EmailVerificationTokenRepository
            .Received(1)
            .TryConsumeAsync(stored.Id, Arg.Any<CancellationToken>());
        await ctx.UserRepository
            .Received(1)
            .MarkEmailVerifiedAsync(stored.UserId, Arg.Any<CancellationToken>());
        ctx.AccessTokenService.Received(1).Generate(user.Id, user.Email);
        await ctx.RefreshTokenRepository
            .Received(1)
            .StoreAsync(
                user.Id,
                "hashed-refresh-token",
                Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task VerifyEmailAsync_UnknownToken_ReturnsInvalidVerificationToken()
    {
        var ctx = CreateSut();

        ctx.EmailVerificationTokenRepository
            .FindByTokenHashAsync(default!, default)
            .ReturnsForAnyArgs(
                (Func<CallInfo, Task<DomainEmailVerificationToken?>>)(
                    _ => Task.FromResult<DomainEmailVerificationToken?>(null)
                )
            );

        var result = await ctx.Sut.VerifyEmailAsync("missing-token");

        result.Should().BeOfType<AuthResult.InvalidVerificationToken>();
        await ctx.EmailVerificationTokenRepository
            .Received(1)
            .FindByTokenHashAsync(TokenHasher.Hash("missing-token"), Arg.Any<CancellationToken>());
        await ctx.EmailVerificationTokenRepository
            .DidNotReceiveWithAnyArgs()
            .TryConsumeAsync(default, default);
        await ctx.UserRepository.DidNotReceiveWithAnyArgs().MarkEmailVerifiedAsync(default, default);
        ctx.AccessTokenService.DidNotReceiveWithAnyArgs().Generate(default, default!);
        ctx.SecureTokenGenerator.DidNotReceiveWithAnyArgs().Generate();
        await ctx.RefreshTokenRepository
            .DidNotReceiveWithAnyArgs()
            .StoreAsync(default, default!, default, default);
    }

    [Fact]
    public async Task VerifyEmailAsync_ConsumedToken_ReturnsInvalidVerificationToken()
    {
        var ctx = CreateSut();
        var stored = CreateEmailVerificationToken(
            Guid.NewGuid(),
            TokenHasher.Hash("consumed-token"),
            consumedAt: DateTimeOffset.UtcNow.AddMinutes(-1)
        );

        ctx.EmailVerificationTokenRepository
            .FindByTokenHashAsync(default!, default)
            .ReturnsForAnyArgs(
                (Func<CallInfo, Task<DomainEmailVerificationToken?>>)(
                    _ => Task.FromResult<DomainEmailVerificationToken?>(stored)
                )
            );

        var result = await ctx.Sut.VerifyEmailAsync("consumed-token");

        result.Should().BeOfType<AuthResult.InvalidVerificationToken>();
        await ctx.EmailVerificationTokenRepository
            .DidNotReceiveWithAnyArgs()
            .TryConsumeAsync(default, default);
        await ctx.UserRepository.DidNotReceiveWithAnyArgs().MarkEmailVerifiedAsync(default, default);
        ctx.AccessTokenService.DidNotReceiveWithAnyArgs().Generate(default, default!);
        ctx.SecureTokenGenerator.DidNotReceiveWithAnyArgs().Generate();
    }

    [Fact]
    public async Task VerifyEmailAsync_ExpiredToken_ReturnsVerificationTokenExpired()
    {
        var ctx = CreateSut();
        var stored = CreateEmailVerificationToken(
            Guid.NewGuid(),
            TokenHasher.Hash("expired-token"),
            expiresAt: DateTimeOffset.UtcNow.AddMinutes(-1)
        );

        ctx.EmailVerificationTokenRepository
            .FindByTokenHashAsync(default!, default)
            .ReturnsForAnyArgs(
                (Func<CallInfo, Task<DomainEmailVerificationToken?>>)(
                    _ => Task.FromResult<DomainEmailVerificationToken?>(stored)
                )
            );

        var result = await ctx.Sut.VerifyEmailAsync("expired-token");

        result.Should().BeOfType<AuthResult.VerificationTokenExpired>();
        await ctx.EmailVerificationTokenRepository
            .DidNotReceiveWithAnyArgs()
            .TryConsumeAsync(default, default);
        await ctx.UserRepository.DidNotReceiveWithAnyArgs().MarkEmailVerifiedAsync(default, default);
        ctx.AccessTokenService.DidNotReceiveWithAnyArgs().Generate(default, default!);
        ctx.SecureTokenGenerator.DidNotReceiveWithAnyArgs().Generate();
    }

    [Fact]
    public async Task VerifyEmailAsync_ConsumeRace_ReturnsInvalidVerificationToken()
    {
        var ctx = CreateSut();
        var stored = CreateEmailVerificationToken(Guid.NewGuid(), TokenHasher.Hash("race-token"));

        ctx.EmailVerificationTokenRepository
            .FindByTokenHashAsync(default!, default)
            .ReturnsForAnyArgs(
                (Func<CallInfo, Task<DomainEmailVerificationToken?>>)(
                    _ => Task.FromResult<DomainEmailVerificationToken?>(stored)
                )
            );
        ctx.EmailVerificationTokenRepository
            .TryConsumeAsync(default, default)
            .ReturnsForAnyArgs((Func<CallInfo, Task<bool>>)(_ => Task.FromResult(false)));

        var result = await ctx.Sut.VerifyEmailAsync("race-token");

        result.Should().BeOfType<AuthResult.InvalidVerificationToken>();
        await ctx.EmailVerificationTokenRepository
            .Received(1)
            .TryConsumeAsync(stored.Id, Arg.Any<CancellationToken>());
        await ctx.UserRepository.DidNotReceiveWithAnyArgs().MarkEmailVerifiedAsync(default, default);
        ctx.AccessTokenService.DidNotReceiveWithAnyArgs().Generate(default, default!);
        ctx.SecureTokenGenerator.DidNotReceiveWithAnyArgs().Generate();
    }

    [Fact]
    public async Task ResendVerificationAsync_UnverifiedUser_DeletesOldTokensStartsWorkflow()
    {
        var ctx = CreateSut();
        var user = CreateUser(
            email: "test@example.com",
            passwordHash: "hashed-password",
            emailVerified: false
        );

        ctx.UserRepository
            .GetByEmailAsync(default!, default)
            .ReturnsForAnyArgs(
                (Func<CallInfo, Task<DomainUser?>>)(_ => Task.FromResult<DomainUser?>(user))
            );
        ctx.SecureTokenGenerator.Generate().Returns(("verification-token", "hashed-verification-token"));

        var result = await ctx.Sut.ResendVerificationAsync(" Test@Example.COM ");

        result.Should().BeOfType<AuthResult.EmailVerificationSent>();
        await ctx.UserRepository
            .Received(1)
            .GetByEmailAsync("test@example.com", Arg.Any<CancellationToken>());
        await ctx.EmailVerificationTokenRepository
            .Received(1)
            .DeleteAllForUserAsync(user.Id, Arg.Any<CancellationToken>());
        await ctx.EmailVerificationTokenRepository
            .Received(1)
            .StoreAsync(
                user.Id,
                "hashed-verification-token",
                Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>()
            );
        await ctx.EmailWorkflowStarter
            .Received(1)
            .StartVerificationWorkflowAsync(
                user.Id,
                user.Email,
                "verification-token",
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task ResendVerificationAsync_VerifiedUser_ReturnsSentButDoesNothing()
    {
        var ctx = CreateSut();
        var user = CreateUser(email: "test@example.com", passwordHash: "hashed-password");

        ctx.UserRepository
            .GetByEmailAsync(default!, default)
            .ReturnsForAnyArgs(
                (Func<CallInfo, Task<DomainUser?>>)(_ => Task.FromResult<DomainUser?>(user))
            );

        var result = await ctx.Sut.ResendVerificationAsync("test@example.com");

        result.Should().BeOfType<AuthResult.EmailVerificationSent>();
        await ctx.EmailVerificationTokenRepository
            .DidNotReceiveWithAnyArgs()
            .DeleteAllForUserAsync(default, default);
        await ctx.EmailVerificationTokenRepository
            .DidNotReceiveWithAnyArgs()
            .StoreAsync(default, default!, default, default);
        await ctx.EmailWorkflowStarter
            .DidNotReceiveWithAnyArgs()
            .StartVerificationWorkflowAsync(default, default!, default!, default);
        ctx.SecureTokenGenerator.DidNotReceiveWithAnyArgs().Generate();
    }

    [Fact]
    public async Task ResendVerificationAsync_MissingUser_ReturnsSentButDoesNothing()
    {
        var ctx = CreateSut();

        ctx.UserRepository
            .GetByEmailAsync(default!, default)
            .ReturnsForAnyArgs(
                (Func<CallInfo, Task<DomainUser?>>)(_ => Task.FromResult<DomainUser?>(null))
            );

        var result = await ctx.Sut.ResendVerificationAsync("missing@example.com");

        result.Should().BeOfType<AuthResult.EmailVerificationSent>();
        await ctx.EmailVerificationTokenRepository
            .DidNotReceiveWithAnyArgs()
            .DeleteAllForUserAsync(default, default);
        await ctx.EmailVerificationTokenRepository
            .DidNotReceiveWithAnyArgs()
            .StoreAsync(default, default!, default, default);
        await ctx.EmailWorkflowStarter
            .DidNotReceiveWithAnyArgs()
            .StartVerificationWorkflowAsync(default, default!, default!, default);
        ctx.SecureTokenGenerator.DidNotReceiveWithAnyArgs().Generate();
    }

    [Fact]
    public async Task ForgotPasswordAsync_VerifiedUser_DeletesOldTokensStartsWorkflow()
    {
        var ctx = CreateSut();
        var user = CreateUser(email: "test@example.com", passwordHash: "hashed-password");

        ctx.UserRepository
            .GetByEmailAsync(default!, default)
            .ReturnsForAnyArgs(
                (Func<CallInfo, Task<DomainUser?>>)(_ => Task.FromResult<DomainUser?>(user))
            );
        ctx.SecureTokenGenerator.Generate().Returns(("reset-token", "hashed-reset-token"));

        var result = await ctx.Sut.ForgotPasswordAsync(" Test@Example.COM ");

        result.Should().BeOfType<AuthResult.PasswordResetSent>();
        await ctx.UserRepository
            .Received(1)
            .GetByEmailAsync("test@example.com", Arg.Any<CancellationToken>());
        await ctx.PasswordResetTokenRepository
            .Received(1)
            .DeleteAllForUserAsync(user.Id, Arg.Any<CancellationToken>());
        await ctx.PasswordResetTokenRepository
            .Received(1)
            .StoreAsync(
                user.Id,
                "hashed-reset-token",
                Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>()
            );
        await ctx.EmailWorkflowStarter
            .Received(1)
            .StartPasswordResetWorkflowAsync(
                user.Id,
                user.Email,
                "reset-token",
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task ForgotPasswordAsync_UnverifiedUser_ReturnsSentButDoesNothing()
    {
        var ctx = CreateSut();
        var user = CreateUser(
            email: "test@example.com",
            passwordHash: "hashed-password",
            emailVerified: false
        );

        ctx.UserRepository
            .GetByEmailAsync(default!, default)
            .ReturnsForAnyArgs(
                (Func<CallInfo, Task<DomainUser?>>)(_ => Task.FromResult<DomainUser?>(user))
            );

        var result = await ctx.Sut.ForgotPasswordAsync("test@example.com");

        result.Should().BeOfType<AuthResult.PasswordResetSent>();
        await ctx.PasswordResetTokenRepository
            .DidNotReceiveWithAnyArgs()
            .DeleteAllForUserAsync(default, default);
        await ctx.PasswordResetTokenRepository
            .DidNotReceiveWithAnyArgs()
            .StoreAsync(default, default!, default, default);
        await ctx.EmailWorkflowStarter
            .DidNotReceiveWithAnyArgs()
            .StartPasswordResetWorkflowAsync(default, default!, default!, default);
        ctx.SecureTokenGenerator.DidNotReceiveWithAnyArgs().Generate();
    }

    [Fact]
    public async Task ForgotPasswordAsync_MissingUser_ReturnsSentButDoesNothing()
    {
        var ctx = CreateSut();

        ctx.UserRepository
            .GetByEmailAsync(default!, default)
            .ReturnsForAnyArgs(
                (Func<CallInfo, Task<DomainUser?>>)(_ => Task.FromResult<DomainUser?>(null))
            );

        var result = await ctx.Sut.ForgotPasswordAsync("missing@example.com");

        result.Should().BeOfType<AuthResult.PasswordResetSent>();
        await ctx.PasswordResetTokenRepository
            .DidNotReceiveWithAnyArgs()
            .DeleteAllForUserAsync(default, default);
        await ctx.PasswordResetTokenRepository
            .DidNotReceiveWithAnyArgs()
            .StoreAsync(default, default!, default, default);
        await ctx.EmailWorkflowStarter
            .DidNotReceiveWithAnyArgs()
            .StartPasswordResetWorkflowAsync(default, default!, default!, default);
        ctx.SecureTokenGenerator.DidNotReceiveWithAnyArgs().Generate();
    }

    [Fact]
    public async Task ResetPasswordAsync_ValidToken_ReturnsSuccessUpdatesPasswordRevokesAllRefreshTokens()
    {
        var ctx = CreateSut();
        var user = CreateUser(email: "test@example.com", passwordHash: "old-hash");
        var stored = CreatePasswordResetToken(user.Id, TokenHasher.Hash("reset-token"));

        ctx.PasswordResetTokenRepository
            .FindByTokenHashAsync(default!, default)
            .ReturnsForAnyArgs(
                (Func<CallInfo, Task<DomainPasswordResetToken?>>)(
                    _ => Task.FromResult<DomainPasswordResetToken?>(stored)
                )
            );
        ctx.PasswordHasher.Hash("new-password").Returns("new-hashed-password");
        ctx.UserRepository
            .GetByIdAsync(default, default)
            .ReturnsForAnyArgs(
                (Func<CallInfo, Task<DomainUser?>>)(_ => Task.FromResult<DomainUser?>(user))
            );
        ctx.AccessTokenService.Generate(user.Id, user.Email).Returns(TestToken);
        ctx.SecureTokenGenerator.Generate().Returns(("fresh-refresh-token", "fresh-hashed-token"));

        var result = await ctx.Sut.ResetPasswordAsync("reset-token", "new-password");

        var success = result.Should().BeOfType<AuthResult.Success>().Subject;
        success.UserId.Should().Be(user.Id);
        success.Email.Should().Be(user.Email);
        success.AccessToken.Should().Be(TestToken);
        success.RefreshToken.Should().Be("fresh-refresh-token");
        await ctx.PasswordResetTokenRepository
            .Received(1)
            .FindByTokenHashAsync(TokenHasher.Hash("reset-token"), Arg.Any<CancellationToken>());
        await ctx.PasswordResetTokenRepository
            .Received(1)
            .TryConsumeAsync(stored.Id, Arg.Any<CancellationToken>());
        ctx.PasswordHasher.Received(1).Hash("new-password");
        await ctx.UserRepository
            .Received(1)
            .UpdatePasswordHashAsync(
                stored.UserId,
                "new-hashed-password",
                Arg.Any<CancellationToken>()
            );
        await ctx.RefreshTokenRepository
            .Received(1)
            .RevokeAllForUserAsync(stored.UserId, Arg.Any<CancellationToken>());
        await ctx.UserRepository.DidNotReceiveWithAnyArgs().MarkEmailVerifiedAsync(default, default);
        ctx.AccessTokenService.Received(1).Generate(user.Id, user.Email);
        await ctx.RefreshTokenRepository
            .Received(1)
            .StoreAsync(
                user.Id,
                "fresh-hashed-token",
                Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task ResetPasswordAsync_UnknownToken_ReturnsInvalidResetToken()
    {
        var ctx = CreateSut();

        ctx.PasswordResetTokenRepository
            .FindByTokenHashAsync(default!, default)
            .ReturnsForAnyArgs(
                (Func<CallInfo, Task<DomainPasswordResetToken?>>)(
                    _ => Task.FromResult<DomainPasswordResetToken?>(null)
                )
            );

        var result = await ctx.Sut.ResetPasswordAsync("missing-token", "new-password");

        result.Should().BeOfType<AuthResult.InvalidResetToken>();
        await ctx.PasswordResetTokenRepository
            .Received(1)
            .FindByTokenHashAsync(TokenHasher.Hash("missing-token"), Arg.Any<CancellationToken>());
        await ctx.PasswordResetTokenRepository
            .DidNotReceiveWithAnyArgs()
            .TryConsumeAsync(default, default);
        ctx.PasswordHasher.DidNotReceiveWithAnyArgs().Hash(default!);
        await ctx.UserRepository
            .DidNotReceiveWithAnyArgs()
            .UpdatePasswordHashAsync(default, default!, default);
        await ctx.RefreshTokenRepository
            .DidNotReceiveWithAnyArgs()
            .RevokeAllForUserAsync(default, default);
        ctx.AccessTokenService.DidNotReceiveWithAnyArgs().Generate(default, default!);
        ctx.SecureTokenGenerator.DidNotReceiveWithAnyArgs().Generate();
    }

    [Fact]
    public async Task ResetPasswordAsync_ConsumedToken_ReturnsInvalidResetToken()
    {
        var ctx = CreateSut();
        var stored = CreatePasswordResetToken(
            Guid.NewGuid(),
            TokenHasher.Hash("consumed-reset-token"),
            consumedAt: DateTimeOffset.UtcNow.AddMinutes(-1)
        );

        ctx.PasswordResetTokenRepository
            .FindByTokenHashAsync(default!, default)
            .ReturnsForAnyArgs(
                (Func<CallInfo, Task<DomainPasswordResetToken?>>)(
                    _ => Task.FromResult<DomainPasswordResetToken?>(stored)
                )
            );

        var result = await ctx.Sut.ResetPasswordAsync("consumed-reset-token", "new-password");

        result.Should().BeOfType<AuthResult.InvalidResetToken>();
        await ctx.PasswordResetTokenRepository
            .DidNotReceiveWithAnyArgs()
            .TryConsumeAsync(default, default);
        ctx.PasswordHasher.DidNotReceiveWithAnyArgs().Hash(default!);
        await ctx.UserRepository
            .DidNotReceiveWithAnyArgs()
            .UpdatePasswordHashAsync(default, default!, default);
        await ctx.RefreshTokenRepository
            .DidNotReceiveWithAnyArgs()
            .RevokeAllForUserAsync(default, default);
        ctx.AccessTokenService.DidNotReceiveWithAnyArgs().Generate(default, default!);
        ctx.SecureTokenGenerator.DidNotReceiveWithAnyArgs().Generate();
    }

    [Fact]
    public async Task ResetPasswordAsync_ExpiredToken_ReturnsResetTokenExpired()
    {
        var ctx = CreateSut();
        var stored = CreatePasswordResetToken(
            Guid.NewGuid(),
            TokenHasher.Hash("expired-reset-token"),
            expiresAt: DateTimeOffset.UtcNow.AddMinutes(-1)
        );

        ctx.PasswordResetTokenRepository
            .FindByTokenHashAsync(default!, default)
            .ReturnsForAnyArgs(
                (Func<CallInfo, Task<DomainPasswordResetToken?>>)(
                    _ => Task.FromResult<DomainPasswordResetToken?>(stored)
                )
            );

        var result = await ctx.Sut.ResetPasswordAsync("expired-reset-token", "new-password");

        result.Should().BeOfType<AuthResult.ResetTokenExpired>();
        await ctx.PasswordResetTokenRepository
            .DidNotReceiveWithAnyArgs()
            .TryConsumeAsync(default, default);
        ctx.PasswordHasher.DidNotReceiveWithAnyArgs().Hash(default!);
        await ctx.UserRepository
            .DidNotReceiveWithAnyArgs()
            .UpdatePasswordHashAsync(default, default!, default);
        await ctx.RefreshTokenRepository
            .DidNotReceiveWithAnyArgs()
            .RevokeAllForUserAsync(default, default);
        ctx.AccessTokenService.DidNotReceiveWithAnyArgs().Generate(default, default!);
        ctx.SecureTokenGenerator.DidNotReceiveWithAnyArgs().Generate();
    }

    [Fact]
    public async Task ResetPasswordAsync_ConsumeRace_ReturnsInvalidResetToken()
    {
        var ctx = CreateSut();
        var stored = CreatePasswordResetToken(Guid.NewGuid(), TokenHasher.Hash("race-reset-token"));

        ctx.PasswordResetTokenRepository
            .FindByTokenHashAsync(default!, default)
            .ReturnsForAnyArgs(
                (Func<CallInfo, Task<DomainPasswordResetToken?>>)(
                    _ => Task.FromResult<DomainPasswordResetToken?>(stored)
                )
            );
        ctx.PasswordResetTokenRepository
            .TryConsumeAsync(default, default)
            .ReturnsForAnyArgs((Func<CallInfo, Task<bool>>)(_ => Task.FromResult(false)));

        var result = await ctx.Sut.ResetPasswordAsync("race-reset-token", "new-password");

        result.Should().BeOfType<AuthResult.InvalidResetToken>();
        await ctx.PasswordResetTokenRepository
            .Received(1)
            .TryConsumeAsync(stored.Id, Arg.Any<CancellationToken>());
        ctx.PasswordHasher.DidNotReceiveWithAnyArgs().Hash(default!);
        await ctx.UserRepository
            .DidNotReceiveWithAnyArgs()
            .UpdatePasswordHashAsync(default, default!, default);
        await ctx.RefreshTokenRepository
            .DidNotReceiveWithAnyArgs()
            .RevokeAllForUserAsync(default, default);
        await ctx.UserRepository.DidNotReceiveWithAnyArgs().MarkEmailVerifiedAsync(default, default);
        ctx.AccessTokenService.DidNotReceiveWithAnyArgs().Generate(default, default!);
        ctx.SecureTokenGenerator.DidNotReceiveWithAnyArgs().Generate();
    }

    private static SutContext CreateSut()
    {
        var userRepository = Substitute.For<IUserRepository>();
        var passwordHasher = Substitute.For<IPasswordHasher>();
        passwordHasher.Hash(default!).ReturnsForAnyArgs("dummy-hash");
        passwordHasher.Verify(default!, default!).ReturnsForAnyArgs(false);
        passwordHasher.DummyHash.Returns("dummy-hash");
        var accessTokenService = Substitute.For<IAccessTokenService>();
        var refreshTokenRepository = Substitute.For<IRefreshTokenRepository>();
        var refreshTokenGenerator = Substitute.For<ISecureTokenGenerator>();
        var emailVerificationTokenRepository = Substitute.For<IEmailVerificationTokenRepository>();
        var passwordResetTokenRepository = Substitute.For<IPasswordResetTokenRepository>();
        var emailWorkflowStarter = Substitute.For<IEmailWorkflowStarter>();

        userRepository
            .MarkEmailVerifiedAsync(default, default)
            .ReturnsForAnyArgs((Func<CallInfo, Task>)(_ => Task.CompletedTask));
        userRepository
            .UpdatePasswordHashAsync(default, default!, default)
            .ReturnsForAnyArgs((Func<CallInfo, Task>)(_ => Task.CompletedTask));

        refreshTokenRepository
            .StoreAsync(default, default!, default, default)
            .ReturnsForAnyArgs((Func<CallInfo, Task>)(_ => Task.CompletedTask));
        refreshTokenRepository
            .TryRevokeAsync(default, default)
            .ReturnsForAnyArgs((Func<CallInfo, Task<bool>>)(_ => Task.FromResult(true)));
        refreshTokenRepository
            .RevokeAllForUserAsync(default, default)
            .ReturnsForAnyArgs((Func<CallInfo, Task>)(_ => Task.CompletedTask));

        emailVerificationTokenRepository
            .StoreAsync(default, default!, default, default)
            .ReturnsForAnyArgs((Func<CallInfo, Task>)(_ => Task.CompletedTask));
        emailVerificationTokenRepository
            .TryConsumeAsync(default, default)
            .ReturnsForAnyArgs((Func<CallInfo, Task<bool>>)(_ => Task.FromResult(true)));
        emailVerificationTokenRepository
            .DeleteAllForUserAsync(default, default)
            .ReturnsForAnyArgs((Func<CallInfo, Task>)(_ => Task.CompletedTask));

        passwordResetTokenRepository
            .StoreAsync(default, default!, default, default)
            .ReturnsForAnyArgs((Func<CallInfo, Task>)(_ => Task.CompletedTask));
        passwordResetTokenRepository
            .TryConsumeAsync(default, default)
            .ReturnsForAnyArgs((Func<CallInfo, Task<bool>>)(_ => Task.FromResult(true)));
        passwordResetTokenRepository
            .DeleteAllForUserAsync(default, default)
            .ReturnsForAnyArgs((Func<CallInfo, Task>)(_ => Task.CompletedTask));

        emailWorkflowStarter
            .StartVerificationWorkflowAsync(default, default!, default!, default)
            .ReturnsForAnyArgs((Func<CallInfo, Task>)(_ => Task.CompletedTask));
        emailWorkflowStarter
            .StartPasswordResetWorkflowAsync(default, default!, default!, default)
            .ReturnsForAnyArgs((Func<CallInfo, Task>)(_ => Task.CompletedTask));

        var sut = new AuthService(
            userRepository,
            passwordHasher,
            accessTokenService,
            refreshTokenRepository,
            refreshTokenGenerator,
            emailVerificationTokenRepository,
            passwordResetTokenRepository,
            emailWorkflowStarter
        );

        return new SutContext(
            sut,
            userRepository,
            passwordHasher,
            accessTokenService,
            refreshTokenRepository,
            refreshTokenGenerator,
            emailVerificationTokenRepository,
            passwordResetTokenRepository,
            emailWorkflowStarter
        );
    }

    private static DomainUser CreateUser(string email, string passwordHash, bool emailVerified = true) =>
        new()
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = passwordHash,
            EmailVerified = emailVerified,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

    private static DomainRefreshToken CreateRefreshToken(
        Guid userId,
        string tokenHash,
        DateTimeOffset? expiresAt = null,
        DateTimeOffset? revokedAt = null
    ) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt ?? DateTimeOffset.UtcNow.AddDays(1),
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            RevokedAt = revokedAt,
        };

    private static DomainEmailVerificationToken CreateEmailVerificationToken(
        Guid userId,
        string tokenHash,
        DateTimeOffset? expiresAt = null,
        DateTimeOffset? consumedAt = null
    ) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt ?? DateTimeOffset.UtcNow.AddHours(1),
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            ConsumedAt = consumedAt,
        };

    private static DomainPasswordResetToken CreatePasswordResetToken(
        Guid userId,
        string tokenHash,
        DateTimeOffset? expiresAt = null,
        DateTimeOffset? consumedAt = null
    ) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt ?? DateTimeOffset.UtcNow.AddHours(1),
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            ConsumedAt = consumedAt,
        };

    private static bool IsSevenDayExpiry(
        DateTimeOffset expiresAt,
        DateTimeOffset before,
        DateTimeOffset after
    ) => expiresAt >= before.AddDays(7) && expiresAt <= after.AddDays(7).AddSeconds(1);

    private sealed record SutContext(
        AuthService Sut,
        IUserRepository UserRepository,
        IPasswordHasher PasswordHasher,
        IAccessTokenService AccessTokenService,
        IRefreshTokenRepository RefreshTokenRepository,
        ISecureTokenGenerator SecureTokenGenerator,
        IEmailVerificationTokenRepository EmailVerificationTokenRepository,
        IPasswordResetTokenRepository PasswordResetTokenRepository,
        IEmailWorkflowStarter EmailWorkflowStarter
    );
}
