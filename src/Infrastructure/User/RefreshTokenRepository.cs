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

    public async Task<bool> TryRevokeAsync(
        Guid refreshTokenId,
        CancellationToken cancellationToken = default
    )
    {
        var rows = await db.RefreshTokens
            .Where(t => t.Id == refreshTokenId && t.RevokedAt == null)
            .ExecuteUpdateAsync(
                setter => setter.SetProperty(t => t.RevokedAt, DateTimeOffset.UtcNow),
                cancellationToken
            );
        return rows > 0;
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
