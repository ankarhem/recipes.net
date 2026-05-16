using App.Recipes.Ports;
using Domain.Recipes;

namespace App.Recipes;

public sealed class RecipeFavoriteService(
    IRecipeCollectionRepository collectionRepository,
    IRecipeRepository recipeRepository,
    IUnitOfWork unitOfWork,
    Domain.IClock clock
) : IRecipeFavoriteService
{
    public async Task<ToggleRecipeFavoriteResult> ToggleFavoriteAsync(
        Guid userId,
        Guid recipeId,
        CancellationToken cancellationToken = default
    )
    {
        var typedRecipeId = new RecipeId(recipeId);
        var exists = await recipeRepository.ExistsAsync(typedRecipeId, cancellationToken);
        if (!exists)
        {
            return new ToggleRecipeFavoriteResult.RecipeNotFound();
        }

        var ownerId = new RecipeCollectionOwnerId(userId);
        var collection = await collectionRepository.GetDefaultFavoritesAsync(
            ownerId,
            cancellationToken
        );
        if (collection is null)
        {
            collection = RecipeCollection.CreateFavorites(ownerId, clock.UtcNow);
        }

        var isFavorite = collection.ToggleRecipe(typedRecipeId, clock.UtcNow);
        await collectionRepository.SaveAsync(collection, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new ToggleRecipeFavoriteResult.Success(isFavorite);
    }

    public async Task<IReadOnlyList<Guid>> ListFavoriteRecipeIdsAsync(
        Guid userId,
        CancellationToken cancellationToken = default
    )
    {
        var collection = await collectionRepository.GetDefaultFavoritesAsync(
            new RecipeCollectionOwnerId(userId),
            cancellationToken
        );

        return collection?.Items.Select(i => i.RecipeId.Value).ToList() ?? [];
    }
}
