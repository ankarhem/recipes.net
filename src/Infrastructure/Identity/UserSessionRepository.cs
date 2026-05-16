using App.Identity;
using Domain;
using Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Identity;

public sealed class UserSessionRepository(AppDbContext db, IClock clock) : IUserSessionRepository
{
    public Task<UserSession?> GetByTokenHashAsync(
        TokenHash hash,
        CancellationToken cancellationToken = default
    ) =>
        db.UserSessions.SingleOrDefaultAsync(s => s.TokenHash == hash, cancellationToken);

    public async Task<IReadOnlyList<UserSession>> GetActiveByUserIdAsync(
        UserId userId,
        CancellationToken cancellationToken = default
    )
    {
        var now = clock.UtcNow;
        return await db
            .UserSessions.AsNoTracking()
            .Where(s => s.UserId == userId && s.RevokedAt == null && s.ExpiresAt > now)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(UserSession session, CancellationToken cancellationToken = default)
    {
        await db.UserSessions.AddAsync(session, cancellationToken);
    }

    public async Task RevokeAllForUserAsync(
        UserId userId,
        CancellationToken cancellationToken = default
    )
    {
        var now = clock.UtcNow;
        await db
            .UserSessions.Where(s => s.UserId == userId && s.RevokedAt == null)
            .ExecuteUpdateAsync(
                setter => setter.SetProperty(s => s.RevokedAt, now),
                cancellationToken
            );
    }

}
