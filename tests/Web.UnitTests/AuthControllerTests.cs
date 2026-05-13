using App.Auth;
using AwesomeAssertions;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using NSubstitute.Core;
using Web.Controllers;
using Web.Models;
using Xunit;

namespace Web.Tests;

public class AuthControllerTests
{
    private readonly IAuthService _service = Substitute.For<IAuthService>();

    [Fact]
    public async Task Register_Success_Returns201WithTokens()
    {
        var userId = Guid.NewGuid();
        var email = "user@example.com";
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(15);
        _service
            .RegisterAsync(default!, default!, default)
            .ReturnsForAnyArgs(
                (Func<CallInfo, Task<AuthResult>>)(
                    _ =>
                        Task.FromResult<AuthResult>(
                            new AuthResult.Success(
                                userId,
                                email,
                                new AccessToken { Token = "jwt-token", ExpiresAt = expiresAt },
                                "refresh-token"
                            )
                        )
                )
            );
        var controller = new AuthController(_service);
        var request = new RegisterRequest { Email = email, Password = "password123" };

        var result = await controller.Register(request, CancellationToken.None);

        var created = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        created.StatusCode.Should().Be(201);
        var response = created.Value.Should().BeOfType<AuthResponse>().Subject;
        response.UserId.Should().Be(userId);
        response.Email.Should().Be(email);
        response.AccessToken.Should().Be("jwt-token");
        response.TokenType.Should().Be("Bearer");
        response.ExpiresAt.Should().Be(expiresAt);
        response.RefreshToken.Should().Be("refresh-token");
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns409()
    {
        _service
            .RegisterAsync(default!, default!, default)
            .ReturnsForAnyArgs(
                (Func<CallInfo, Task<AuthResult>>)(
                    _ => Task.FromResult<AuthResult>(new AuthResult.EmailAlreadyRegistered())
                )
            );
        var controller = new AuthController(_service);
        var request = new RegisterRequest { Email = "user@example.com", Password = "password123" };

        var result = await controller.Register(request, CancellationToken.None);

        var conflict = result.Should().BeOfType<ConflictObjectResult>().Subject;
        conflict.StatusCode.Should().Be(409);
    }

    [Fact]
    public async Task Login_Success_Returns200WithTokens()
    {
        var userId = Guid.NewGuid();
        var email = "user@example.com";
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(15);
        _service
            .LoginAsync(default!, default!, default)
            .ReturnsForAnyArgs(
                (Func<CallInfo, Task<AuthResult>>)(
                    _ =>
                        Task.FromResult<AuthResult>(
                            new AuthResult.Success(
                                userId,
                                email,
                                new AccessToken { Token = "jwt-token", ExpiresAt = expiresAt },
                                "refresh-token"
                            )
                        )
                )
            );
        var controller = new AuthController(_service);
        var request = new LoginRequest { Email = email, Password = "password123" };

        var result = await controller.Login(request, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);
        var response = ok.Value.Should().BeOfType<AuthResponse>().Subject;
        response.UserId.Should().Be(userId);
        response.Email.Should().Be(email);
        response.AccessToken.Should().Be("jwt-token");
        response.TokenType.Should().Be("Bearer");
        response.ExpiresAt.Should().Be(expiresAt);
        response.RefreshToken.Should().Be("refresh-token");
    }

    [Fact]
    public async Task Login_InvalidCredentials_Returns401()
    {
        _service
            .LoginAsync(default!, default!, default)
            .ReturnsForAnyArgs(
                (Func<CallInfo, Task<AuthResult>>)(
                    _ => Task.FromResult<AuthResult>(new AuthResult.InvalidCredentials())
                )
            );
        var controller = new AuthController(_service);
        var request = new LoginRequest { Email = "user@example.com", Password = "wrong-password" };

        var result = await controller.Login(request, CancellationToken.None);

        var unauthorized = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthorized.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task Refresh_ValidToken_Returns200WithTokens()
    {
        var userId = Guid.NewGuid();
        var email = "user@example.com";
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(15);
        _service
            .RefreshAsync(default!, default)
            .ReturnsForAnyArgs(
                (Func<CallInfo, Task<AuthResult>>)(
                    _ =>
                        Task.FromResult<AuthResult>(
                            new AuthResult.Success(
                                userId,
                                email,
                                new AccessToken { Token = "new-jwt-token", ExpiresAt = expiresAt },
                                "new-refresh-token"
                            )
                        )
                )
            );
        var controller = new AuthController(_service);
        var request = new RefreshRequest { RefreshToken = "refresh-token" };

        var result = await controller.Refresh(request, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);
        var response = ok.Value.Should().BeOfType<AuthResponse>().Subject;
        response.UserId.Should().Be(userId);
        response.Email.Should().Be(email);
        response.AccessToken.Should().Be("new-jwt-token");
        response.TokenType.Should().Be("Bearer");
        response.ExpiresAt.Should().Be(expiresAt);
        response.RefreshToken.Should().Be("new-refresh-token");
    }

    [Fact]
    public async Task Refresh_InvalidToken_Returns401()
    {
        _service
            .RefreshAsync(default!, default)
            .ReturnsForAnyArgs(
                (Func<CallInfo, Task<AuthResult>>)(
                    _ => Task.FromResult<AuthResult>(new AuthResult.InvalidRefreshToken())
                )
            );
        var controller = new AuthController(_service);
        var request = new RefreshRequest { RefreshToken = "expired-refresh-token" };

        var result = await controller.Refresh(request, CancellationToken.None);

        var unauthorized = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthorized.StatusCode.Should().Be(401);
    }
}
