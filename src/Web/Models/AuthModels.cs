using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Web.Models;

public sealed record RegisterRequest
{
    [Description("The user's email address.")]
    [EmailAddress]
    public required string Email { get; init; }

    [Description("The user's password.")]
    [MinLength(8)]
    [MaxLength(72)]
    public required string Password { get; init; }
}

public sealed record LoginRequest
{
    [Description("The user's email address.")]
    [EmailAddress]
    public required string Email { get; init; }

    [Description("The user's password.")]
    [MinLength(8)]
    [MaxLength(72)]
    public required string Password { get; init; }
}

public sealed record AuthResponse
{
    [Description("The user's unique identifier.")]
    public required Guid UserId { get; init; }

    [Description("The user's email address.")]
    public required string Email { get; init; }

    [Description("The JWT access token.")]
    public required string AccessToken { get; init; }

    [Description("The token type.")]
    public required string TokenType { get; init; } = "Bearer";

    [Description("When the access token expires.")]
    public required DateTimeOffset ExpiresAt { get; init; }

    [Description("The refresh token for obtaining new access tokens.")]
    public required string RefreshToken { get; init; }
}

public sealed record RefreshRequest
{
    [Description("The refresh token.")]
    public required string RefreshToken { get; init; }
}

public sealed record RegistrationResponse
{
    [Description("The user's unique identifier.")]
    public required Guid UserId { get; init; }

    [Description("The user's email address.")]
    public required string Email { get; init; }
}

public sealed record VerifyEmailRequest
{
    [Description("The email verification token.")]
    [MinLength(1)]
    [MaxLength(512)]
    public required string Token { get; init; }
}

public sealed record ResendVerificationRequest
{
    [Description("The email address to resend verification to.")]
    [EmailAddress]
    public required string Email { get; init; }
}

public sealed record ForgotPasswordRequest
{
    [Description("The email address to send a password reset link to.")]
    [EmailAddress]
    public required string Email { get; init; }
}

public sealed record ResetPasswordRequest
{
    [Description("The password reset token.")]
    [MinLength(1)]
    [MaxLength(512)]
    public required string Token { get; init; }

    [Description("The new password.")]
    [MinLength(8)]
    [MaxLength(72)]
    public required string NewPassword { get; init; }
}
