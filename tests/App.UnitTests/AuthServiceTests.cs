using App.Auth;
using AwesomeAssertions;
using NSubstitute;
using NSubstitute.Core;
using Xunit;
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
    public async Task RegisterAsync_NewEmail_ReturnsSuccessAndStoresRefreshToken()
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
        ctx.AccessTokenService.Generate(user.Id, user.Email).Returns(TestToken);
        ctx.RefreshTokenGenerator.Generate().Returns(("refresh-token", "hashed-refresh-token"));

        var before = DateTimeOffset.UtcNow;
        var result = await ctx.Sut.RegisterAsync(" Test@Example.COM ", "password");
        var after = DateTimeOffset.UtcNow;

        var success = result.Should().BeOfType<AuthResult.Success>().Subject;
        success.UserId.Should().Be(user.Id);
        success.Email.Should().Be("test@example.com");
        success.AccessToken.Should().Be(TestToken);
        success.RefreshToken.Should().Be("refresh-token");
        await ctx.UserRepository
            .Received(1)
            .GetByEmailAsync("test@example.com", Arg.Any<CancellationToken>());
        await ctx.UserRepository
            .Received(1)
            .CreateAsync("test@example.com", "hashed-password", Arg.Any<CancellationToken>());
        ctx.RefreshTokenGenerator.Received(1).Generate();
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
        ctx.RefreshTokenGenerator.DidNotReceiveWithAnyArgs().Generate();
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
        ctx.RefreshTokenGenerator.Generate().Returns(("refresh-token", "hashed-refresh-token"));

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
        ctx.RefreshTokenGenerator.Received(1).Generate();
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
        ctx.PasswordHasher.DidNotReceiveWithAnyArgs().Verify(default!, default!);
        ctx.AccessTokenService.DidNotReceiveWithAnyArgs().Generate(default, default!);
        ctx.RefreshTokenGenerator.DidNotReceiveWithAnyArgs().Generate();
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
        ctx.RefreshTokenGenerator.DidNotReceiveWithAnyArgs().Generate();
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
        ctx.RefreshTokenGenerator.Generate().Returns(("new-refresh-token", "new-hashed-token"));

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
            .RevokeAsync(refreshToken.Id, Arg.Any<CancellationToken>());
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
        await ctx.RefreshTokenRepository.DidNotReceiveWithAnyArgs().RevokeAsync(default, default);
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
        await ctx.RefreshTokenRepository.DidNotReceiveWithAnyArgs().RevokeAsync(default, default);
        ctx.AccessTokenService.DidNotReceiveWithAnyArgs().Generate(default, default!);
        ctx.RefreshTokenGenerator.DidNotReceiveWithAnyArgs().Generate();
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
        await ctx.RefreshTokenRepository.DidNotReceiveWithAnyArgs().RevokeAsync(default, default);
        ctx.AccessTokenService.DidNotReceiveWithAnyArgs().Generate(default, default!);
        ctx.RefreshTokenGenerator.DidNotReceiveWithAnyArgs().Generate();
    }

    private static SutContext CreateSut()
    {
        var userRepository = Substitute.For<IUserRepository>();
        var passwordHasher = Substitute.For<IPasswordHasher>();
        var accessTokenService = Substitute.For<IAccessTokenService>();
        var refreshTokenRepository = Substitute.For<IRefreshTokenRepository>();
        var refreshTokenGenerator = Substitute.For<IRefreshTokenGenerator>();

        refreshTokenRepository
            .StoreAsync(default, default!, default, default)
            .ReturnsForAnyArgs((Func<CallInfo, Task>)(_ => Task.CompletedTask));
        refreshTokenRepository
            .RevokeAsync(default, default)
            .ReturnsForAnyArgs((Func<CallInfo, Task>)(_ => Task.CompletedTask));
        refreshTokenRepository
            .RevokeAllForUserAsync(default, default)
            .ReturnsForAnyArgs((Func<CallInfo, Task>)(_ => Task.CompletedTask));

        var sut = new AuthService(
            userRepository,
            passwordHasher,
            accessTokenService,
            refreshTokenRepository,
            refreshTokenGenerator
        );

        return new SutContext(
            sut,
            userRepository,
            passwordHasher,
            accessTokenService,
            refreshTokenRepository,
            refreshTokenGenerator
        );
    }

    private static DomainUser CreateUser(string email, string passwordHash) =>
        new()
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = passwordHash,
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
        IRefreshTokenGenerator RefreshTokenGenerator
    );
}
