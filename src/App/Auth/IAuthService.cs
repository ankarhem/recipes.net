namespace App.Auth;

public interface IAuthService
{
    Task<AuthResult> RegisterAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default
    );
    Task<AuthResult> LoginAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default
    );
    Task<AuthResult> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default);
}
