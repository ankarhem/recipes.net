using Domain.Identity;

namespace App.Identity.Ports;

public interface IUserSessionRepository
{
    Task<UserSession?> GetByTokenHashAsync(
        TokenHash hash,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<UserSession>> GetActiveByUserIdAsync(
        UserId userId,
        CancellationToken cancellationToken = default
    );

    Task AddAsync(UserSession session, CancellationToken cancellationToken = default);

    Task RevokeAllForUserAsync(UserId userId, CancellationToken cancellationToken = default);
}
