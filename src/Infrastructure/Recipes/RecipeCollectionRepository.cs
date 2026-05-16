using App.Recipes.Ports;
using Domain.Recipes;
using Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Recipes;

public sealed class RecipeCollectionRepository(AppDbContext db) : IRecipeCollectionRepository
{
    public async Task<RecipeCollection?> GetByIdAsync(
        RecipeCollectionId id,
        CancellationToken cancellationToken = default
    )
    {
        return await db.RecipeCollections
            .Include(c => c.Items)
            .SingleOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<RecipeCollection?> GetDefaultFavoritesAsync(
        RecipeCollectionOwnerId ownerId,
        CancellationToken cancellationToken = default
    )
    {
        return await db.RecipeCollections
            .Include(c => c.Items)
            .SingleOrDefaultAsync(
                c => c.OwnerId == ownerId && c.Kind == RecipeCollectionKind.Favorites,
                cancellationToken
            );
    }

    public async Task SaveAsync(
        RecipeCollection collection,
        CancellationToken cancellationToken = default
    )
    {
        if (db.Entry(collection).State is not EntityState.Detached)
        {
            return;
        }

        var existing = await db.RecipeCollections
            .Include(c => c.Items)
            .SingleOrDefaultAsync(c => c.Id == collection.Id, cancellationToken);

        if (existing is null)
        {
            await db.RecipeCollections.AddAsync(collection, cancellationToken);
        }
    }
}
