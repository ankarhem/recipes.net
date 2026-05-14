namespace App.Identity;

public abstract record AuthResult
{
    public sealed record Success(
        Guid UserId,
        string Email,
        AccessToken AccessToken,
        string RefreshToken
    ) : AuthResult;

    public sealed record RegistrationPending(Guid UserId, string Email) : AuthResult;
    public sealed record InvalidCredentials : AuthResult;
    public sealed record EmailAlreadyRegistered : AuthResult;
    public sealed record InvalidRefreshToken : AuthResult;
    public sealed record EmailNotVerified : AuthResult;
    public sealed record InvalidVerificationToken : AuthResult;
    public sealed record VerificationTokenExpired : AuthResult;
    public sealed record InvalidResetToken : AuthResult;
    public sealed record ResetTokenExpired : AuthResult;
    public sealed record PasswordResetSent : AuthResult;
    public sealed record EmailVerificationSent : AuthResult;
}
