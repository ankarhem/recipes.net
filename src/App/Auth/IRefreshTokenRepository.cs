using DomainRefreshToken = Domain.User.RefreshToken;

namespace App.Auth;

public interface IRefreshTokenRepository
{
    Task StoreAsync(
        Guid userId,
        string tokenHash,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default
    );

    Task<DomainRefreshToken?> FindByTokenHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default
    );

    Task<bool> TryRevokeAsync(Guid refreshTokenId, CancellationToken cancellationToken = default);
    Task RevokeAllForUserAsync(Guid userId, CancellationToken cancellationToken = default);
}
