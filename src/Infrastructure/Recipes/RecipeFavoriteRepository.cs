using App.Recipes;
using Domain.Identity;
using Domain.Recipes;
using Infrastructure.Recipes;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Recipes;

public sealed class RecipeFavoriteRepository(RecipesDbContext db) : IRecipeFavoriteRepository
{
    public async Task<RecipeFavorite?> FindAsync(
        Guid userId,
        Guid recipeId,
        CancellationToken cancellationToken = default
    )
    {
        var typedUserId = new UserId(userId);
        var entity = await db.RecipeFavorites.AsNoTracking()
            .SingleOrDefaultAsync(
                f => f.UserId == typedUserId && f.RecipeId == recipeId,
                cancellationToken
            );

        return entity?.ToDomain();
    }

    public async Task AddAsync(
        Guid userId,
        Guid recipeId,
        CancellationToken cancellationToken = default
    )
    {
        var entity = new RecipeFavoriteEntity
        {
            UserId = new UserId(userId),
            RecipeId = recipeId,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        await db.RecipeFavorites.AddAsync(entity, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveAsync(
        Guid userId,
        Guid recipeId,
        CancellationToken cancellationToken = default
    )
    {
        var typedUserId = new UserId(userId);
        var entity = await db.RecipeFavorites.SingleOrDefaultAsync(
            f => f.UserId == typedUserId && f.RecipeId == recipeId,
            cancellationToken
        );
        if (entity is not null)
        {
            db.RecipeFavorites.Remove(entity);
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<IReadOnlyList<Guid>> ListByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default
    )
    {
        var typedUserId = new UserId(userId);
        return await db.RecipeFavorites.AsNoTracking()
            .Where(f => f.UserId == typedUserId)
            .OrderByDescending(f => f.CreatedAt)
            .Select(f => f.RecipeId)
            .ToListAsync(cancellationToken);
    }
}
