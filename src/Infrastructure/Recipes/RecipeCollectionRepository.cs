using App.Recipes;
using Domain.Recipes;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Recipes;

public sealed class RecipeCollectionRepository(RecipesDbContext db) : IRecipeCollectionRepository
{
    public async Task<RecipeCollection?> GetDefaultFavoritesAsync(
        RecipeCollectionOwnerId ownerId,
        CancellationToken cancellationToken = default
    )
    {
        var entity = await db.RecipeCollections
            .Include(c => c.Items)
            .SingleOrDefaultAsync(
                c => c.OwnerUserId == ownerId.Value && c.Kind == RecipeCollectionKind.Favorites,
                cancellationToken
            );

        return entity?.ToDomain();
    }

    public async Task SaveAsync(
        RecipeCollection collection,
        CancellationToken cancellationToken = default
    )
    {
        var entity = await db.RecipeCollections
            .Include(c => c.Items)
            .SingleOrDefaultAsync(c => c.Id == collection.Id.Value, cancellationToken);

        if (entity is null)
        {
            await db.RecipeCollections.AddAsync(
                RecipeCollectionEntity.FromDomain(collection),
                cancellationToken
            );
            return;
        }

        entity.UpdateFromDomain(collection);
    }
}
