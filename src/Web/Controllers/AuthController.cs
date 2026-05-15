using App.Identity;
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
    /// <response code="201">User registered successfully. Check email for verification.</response>
    /// <response code="409">Email already registered.</response>
    [HttpPost("register")]
    [EnableRateLimiting("auth")]
    [ProducesResponseType<RegistrationResponse>(StatusCodes.Status201Created)]
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
            AuthResult.RegistrationPending rp => CreatedAtAction(
                nameof(Register),
                new { },
                new RegistrationResponse { UserId = rp.UserId, Email = rp.Email }
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
    /// <response code="403">Email not verified.</response>
    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<TwoFactorRequiredResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.LoginAsync(request.Email, request.Password, cancellationToken);

        return result switch
        {
            AuthResult.Success s => Ok(MapToAuthResponse(s)),
            AuthResult.TwoFactorRequired t => Accepted(
                new TwoFactorRequiredResponse(
                    t.UserId,
                    t.ChallengeToken,
                    t.AvailableMethods.ToArray()
                )
            ),
            AuthResult.InvalidCredentials => Unauthorized(new { error = "Invalid credentials." }),
            AuthResult.EmailNotVerified => StatusCode(
                StatusCodes.Status403Forbidden,
                new { error = "Email not verified. Please check your email for a verification link." }
            ),
            _ => StatusCode(StatusCodes.Status500InternalServerError),
        };
    }

    /// <summary>
    /// Verify a two-factor authentication challenge with a TOTP or recovery code.
    /// </summary>
    /// <response code="200">Two-factor verification successful.</response>
    /// <response code="400">Invalid two-factor code.</response>
    /// <response code="401">Invalid or expired challenge token.</response>
    /// <response code="403">Email not verified.</response>
    [HttpPost("verify-totp")]
    [EnableRateLimiting("twofa")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> VerifyTotp(
        VerifyTotpRequest request,
        CancellationToken cancellationToken
    )
    {
        var result = await authService.VerifyTotpAsync(
            request.ChallengeToken,
            request.Code,
            cancellationToken
        );

        return result switch
        {
            AuthResult.Success s => Ok(MapToAuthResponse(s)),
            AuthResult.InvalidChallengeToken => Unauthorized(
                new { error = "Invalid challenge token." }
            ),
            AuthResult.ChallengeTokenExpired => Unauthorized(
                new { error = "Challenge token expired." }
            ),
            AuthResult.InvalidTwoFactorCode => BadRequest(
                new { error = "Invalid two-factor code." }
            ),
            AuthResult.EmailNotVerified => StatusCode(
                StatusCodes.Status403Forbidden,
                new { error = "Email not verified. Please check your email for a verification link." }
            ),
            _ => StatusCode(StatusCodes.Status500InternalServerError),
        };
    }

    /// <summary>
    /// Refresh an access token using a refresh token.
    /// </summary>
    /// <response code="200">Token refreshed successfully.</response>
    /// <response code="401">Invalid or expired refresh token.</response>
    /// <response code="403">Email not verified.</response>
    [HttpPost("refresh")]
    [EnableRateLimiting("auth")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
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
            AuthResult.EmailNotVerified => StatusCode(
                StatusCodes.Status403Forbidden,
                new { error = "Email not verified. Please check your email for a verification link." }
            ),
            _ => StatusCode(StatusCodes.Status500InternalServerError),
        };
    }

    /// <summary>
    /// Verify email address with a verification token.
    /// </summary>
    /// <response code="200">Email verified successfully.</response>
    /// <response code="400">Invalid or expired verification token.</response>
    [HttpPost("verify-email")]
    [EnableRateLimiting("auth")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> VerifyEmail(
        VerifyEmailRequest request,
        CancellationToken cancellationToken
    )
    {
        var result = await authService.VerifyEmailAsync(request.Token, cancellationToken);

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
            AuthResult.InvalidVerificationToken => BadRequest(
                new { error = "Invalid verification token." }
            ),
            AuthResult.VerificationTokenExpired => BadRequest(
                new { error = "Verification token has expired. Please request a new one." }
            ),
            _ => StatusCode(StatusCodes.Status500InternalServerError),
        };
    }

    /// <summary>
    /// Resend email verification.
    /// </summary>
    /// <response code="200">If the email exists and is unverified, a new verification email has been sent.</response>
    [HttpPost("resend-verification")]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ResendVerification(
        ResendVerificationRequest request,
        CancellationToken cancellationToken
    )
    {
        await authService.ResendVerificationAsync(request.Email, cancellationToken);
        return Ok(
            new
            {
                message = "If the email exists and is unverified, a verification email has been sent.",
            }
        );
    }

    /// <summary>
    /// Request a password reset email.
    /// </summary>
    /// <response code="200">If the email exists, a password reset email has been sent.</response>
    [HttpPost("forgot-password")]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ForgotPassword(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken
    )
    {
        await authService.ForgotPasswordAsync(request.Email, cancellationToken);
        return Ok(
            new { message = "If the email exists, a password reset email has been sent." }
        );
    }

    /// <summary>
    /// Reset password with a reset token.
    /// </summary>
    /// <response code="200">Password reset successfully.</response>
    /// <response code="400">Invalid or expired reset token.</response>
    [HttpPost("reset-password")]
    [EnableRateLimiting("auth")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword(
        ResetPasswordRequest request,
        CancellationToken cancellationToken
    )
    {
        var result = await authService.ResetPasswordAsync(
            request.Token,
            request.NewPassword,
            cancellationToken
        );

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
            AuthResult.InvalidResetToken => BadRequest(new { error = "Invalid reset token." }),
            AuthResult.ResetTokenExpired => BadRequest(
                new { error = "Reset token has expired. Please request a new one." }
            ),
            _ => StatusCode(StatusCodes.Status500InternalServerError),
        };
    }

    private static AuthResponse MapToAuthResponse(AuthResult.Success success) =>
        new()
        {
            UserId = success.UserId,
            Email = success.Email,
            AccessToken = success.AccessToken.Token,
            TokenType = "Bearer",
            ExpiresAt = success.AccessToken.ExpiresAt,
            RefreshToken = success.RefreshToken,
        };
}
