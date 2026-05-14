using App.Auth;
using Infrastructure.Recipe;
using Microsoft.EntityFrameworkCore;
using DomainPasswordResetToken = Domain.User.PasswordResetToken;

namespace Infrastructure.User;

public sealed class PasswordResetTokenRepository(RecipesDbContext db)
    : IPasswordResetTokenRepository
{
    public async Task StoreAsync(
        Guid userId,
        string tokenHash,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default
    )
    {
        var entity = new PasswordResetTokenEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        await db.PasswordResetTokens.AddAsync(entity, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<DomainPasswordResetToken?> FindByTokenHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default
    )
    {
        var entity = await db.PasswordResetTokens.AsNoTracking()
            .SingleOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        return entity?.ToDomain();
    }

    public async Task<bool> TryConsumeAsync(Guid tokenId, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var rows = await db.PasswordResetTokens
            .Where(t => t.Id == tokenId && t.ConsumedAt == null && t.ExpiresAt > now)
            .ExecuteUpdateAsync(
                setter => setter.SetProperty(t => t.ConsumedAt, now),
                cancellationToken
            );
        return rows > 0;
    }

    public async Task DeleteAllForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default
    )
    {
        await db.PasswordResetTokens
            .Where(t => t.UserId == userId && t.ConsumedAt == null)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
