namespace App.Auth;

public abstract record AuthResult
{
    public sealed record Success(
        Guid UserId,
        string Email,
        AccessToken AccessToken,
        string RefreshToken
    ) : AuthResult;

    public sealed record InvalidCredentials : AuthResult;
    public sealed record EmailAlreadyRegistered : AuthResult;
    public sealed record InvalidRefreshToken : AuthResult;
}
