using App.Auth;
using Infrastructure.Recipe;
using Microsoft.EntityFrameworkCore;
using DomainRefreshToken = Domain.User.RefreshToken;

namespace Infrastructure.User;

public sealed class RefreshTokenRepository(RecipesDbContext db) : IRefreshTokenRepository
{
    public async Task StoreAsync(
        Guid userId,
        string tokenHash,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default
    )
    {
        var entity = new RefreshTokenEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        await db.RefreshTokens.AddAsync(entity, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<DomainRefreshToken?> FindByTokenHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default
    )
    {
        var entity = await db.RefreshTokens.AsNoTracking()
            .SingleOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        return entity?.ToDomain();
    }

    public async Task RevokeAsync(
        Guid refreshTokenId,
        CancellationToken cancellationToken = default
    )
    {
        var entity = await db.RefreshTokens.FindAsync([refreshTokenId], cancellationToken);
        if (entity is not null)
        {
            entity.RevokedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task RevokeAllForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default
    )
    {
        await db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(
                setter => setter.SetProperty(t => t.RevokedAt, DateTimeOffset.UtcNow),
                cancellationToken
            );
    }
}
