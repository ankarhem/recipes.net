using DomainPasswordResetToken = Domain.User.PasswordResetToken;

namespace App.Auth;

public interface IPasswordResetTokenRepository
{
    Task StoreAsync(
        Guid userId,
        string tokenHash,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default
    );

    Task<DomainPasswordResetToken?> FindByTokenHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default
    );

    Task<bool> TryConsumeAsync(Guid tokenId, CancellationToken cancellationToken = default);
    Task DeleteAllForUserAsync(Guid userId, CancellationToken cancellationToken = default);
}
