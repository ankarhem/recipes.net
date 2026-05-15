using Domain.Recipes;

namespace App.Recipes;

public interface IRecipeCollectionRepository
{
    Task<RecipeCollection?> GetDefaultFavoritesAsync(
        RecipeCollectionOwnerId ownerId,
        CancellationToken cancellationToken = default
    );

    Task SaveAsync(RecipeCollection collection, CancellationToken cancellationToken = default);
}
