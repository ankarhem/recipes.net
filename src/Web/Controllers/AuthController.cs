using App.Auth;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Web.Models;

namespace Web.Controllers;

[ApiController]
[Route("/api/v1/auth")]
[Tags("Auth")]
public class AuthController(IAuthService authService) : ControllerBase
{
    /// <summary>
    /// Register a new user account.
    /// </summary>
    /// <response code="201">User registered successfully.</response>
    /// <response code="409">Email already registered.</response>
    [HttpPost("register")]
    [EnableRateLimiting("auth")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(
        RegisterRequest request,
        CancellationToken cancellationToken
    )
    {
        var result = await authService.RegisterAsync(
            request.Email,
            request.Password,
            cancellationToken
        );

        return result switch
        {
            AuthResult.Success s => CreatedAtAction(
                nameof(Register),
                new { },
                new AuthResponse
                {
                    UserId = s.UserId,
                    Email = s.Email,
                    AccessToken = s.AccessToken.Token,
                    TokenType = "Bearer",
                    ExpiresAt = s.AccessToken.ExpiresAt,
                    RefreshToken = s.RefreshToken,
                }
            ),
            AuthResult.EmailAlreadyRegistered => Conflict(new { error = "Email already registered." }),
            _ => StatusCode(StatusCodes.Status500InternalServerError),
        };
    }

    /// <summary>
    /// Login with email and password.
    /// </summary>
    /// <response code="200">Login successful.</response>
    /// <response code="401">Invalid credentials.</response>
    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.LoginAsync(request.Email, request.Password, cancellationToken);

        return result switch
        {
            AuthResult.Success s => Ok(
                new AuthResponse
                {
                    UserId = s.UserId,
                    Email = s.Email,
                    AccessToken = s.AccessToken.Token,
                    TokenType = "Bearer",
                    ExpiresAt = s.AccessToken.ExpiresAt,
                    RefreshToken = s.RefreshToken,
                }
            ),
            AuthResult.InvalidCredentials => Unauthorized(new { error = "Invalid credentials." }),
            _ => StatusCode(StatusCodes.Status500InternalServerError),
        };
    }

    /// <summary>
    /// Refresh an access token using a refresh token.
    /// </summary>
    /// <response code="200">Token refreshed successfully.</response>
    /// <response code="401">Invalid or expired refresh token.</response>
    [HttpPost("refresh")]
    [EnableRateLimiting("auth")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh(
        RefreshRequest request,
        CancellationToken cancellationToken
    )
    {
        var result = await authService.RefreshAsync(request.RefreshToken, cancellationToken);

        return result switch
        {
            AuthResult.Success s => Ok(
                new AuthResponse
                {
                    UserId = s.UserId,
                    Email = s.Email,
                    AccessToken = s.AccessToken.Token,
                    TokenType = "Bearer",
                    ExpiresAt = s.AccessToken.ExpiresAt,
                    RefreshToken = s.RefreshToken,
                }
            ),
            AuthResult.InvalidRefreshToken => Unauthorized(
                new { error = "Invalid or expired refresh token." }
            ),
            _ => StatusCode(StatusCodes.Status500InternalServerError),
        };
    }
}
