using DomainEmailVerificationToken = Domain.User.EmailVerificationToken;

namespace App.Auth;

public interface IEmailVerificationTokenRepository
{
    Task StoreAsync(
        Guid userId,
        string tokenHash,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default
    );

    Task<DomainEmailVerificationToken?> FindByTokenHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default
    );

    Task<bool> TryConsumeAsync(Guid tokenId, CancellationToken cancellationToken = default);
    Task DeleteAllForUserAsync(Guid userId, CancellationToken cancellationToken = default);
}
