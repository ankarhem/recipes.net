using Domain.Recipes;

namespace App.Recipes.Ports;

public interface IRecipeCollectionRepository
{
    Task<RecipeCollection?> GetByIdAsync(
        RecipeCollectionId id,
        CancellationToken cancellationToken = default
    );

    Task<RecipeCollection?> GetDefaultFavoritesAsync(
        RecipeCollectionOwnerId ownerId,
        CancellationToken cancellationToken = default
    );

    Task SaveAsync(RecipeCollection collection, CancellationToken cancellationToken = default);
}
