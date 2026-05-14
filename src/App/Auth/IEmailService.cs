namespace App.Auth;

public interface IEmailService
{
    Task SendEmailVerificationAsync(
        string email,
        string token,
        CancellationToken cancellationToken = default
    );

    Task SendPasswordResetAsync(
        string email,
        string token,
        CancellationToken cancellationToken = default
    );
}
