namespace App.Identity;

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
    Task<AuthResult> VerifyEmailAsync(
        string token,
        CancellationToken cancellationToken = default
    );
    Task<AuthResult> ResendVerificationAsync(
        string email,
        CancellationToken cancellationToken = default
    );
    Task<AuthResult> ForgotPasswordAsync(
        string email,
        CancellationToken cancellationToken = default
    );
    Task<AuthResult> ResetPasswordAsync(
        string token,
        string newPassword,
        CancellationToken cancellationToken = default
    );
}
