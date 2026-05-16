using App.Recipes.Ports;
using Domain.Recipes;

namespace App.Recipes;

public sealed class RecipeCollectionService(
    IRecipeCollectionRepository collectionRepository,
    IRecipeRepository recipeRepository,
    IUnitOfWork unitOfWork,
    Domain.IClock clock
) : IRecipeCollectionService
{
    public async Task<ToggleRecipeResult> ToggleRecipeAsync(
        Guid collectionId,
        Guid recipeId,
        CancellationToken cancellationToken = default
    )
    {
        var collection = await collectionRepository.GetByIdAsync(
            new RecipeCollectionId(collectionId),
            cancellationToken
        );
        if (collection is null)
        {
            return new ToggleRecipeResult.CollectionNotFound();
        }

        var typedRecipeId = new RecipeId(recipeId);
        if (!await RecipeExistsAsync(typedRecipeId, cancellationToken))
        {
            return new ToggleRecipeResult.RecipeNotFound();
        }

        return await ToggleRecipeAsync(collection, typedRecipeId, cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> ListRecipeIdsAsync(
        Guid collectionId,
        CancellationToken cancellationToken = default
    )
    {
        var collection = await collectionRepository.GetByIdAsync(
            new RecipeCollectionId(collectionId),
            cancellationToken
        );

        return collection is null ? [] : ListRecipeIds(collection);
    }

    public async Task<ToggleRecipeResult> ToggleFavoriteAsync(
        Guid userId,
        Guid recipeId,
        CancellationToken cancellationToken = default
    )
    {
        var typedRecipeId = new RecipeId(recipeId);
        if (!await RecipeExistsAsync(typedRecipeId, cancellationToken))
        {
            return new ToggleRecipeResult.RecipeNotFound();
        }

        var ownerId = new RecipeCollectionOwnerId(userId);
        var collection = await GetOrCreateFavoritesAsync(ownerId, cancellationToken);

        return await ToggleRecipeAsync(collection, typedRecipeId, cancellationToken);
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

        return collection is null ? [] : ListRecipeIds(collection);
    }

    private async Task<ToggleRecipeResult> ToggleRecipeAsync(
        RecipeCollection collection,
        RecipeId recipeId,
        CancellationToken cancellationToken
    )
    {
        var isAdded = collection.ToggleRecipe(recipeId, clock.UtcNow);
        await collectionRepository.SaveAsync(collection, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new ToggleRecipeResult.Success(isAdded);
    }

    private async Task<RecipeCollection> GetOrCreateFavoritesAsync(
        RecipeCollectionOwnerId ownerId,
        CancellationToken cancellationToken
    ) =>
        await collectionRepository.GetDefaultFavoritesAsync(ownerId, cancellationToken)
        ?? RecipeCollection.CreateFavorites(ownerId, clock.UtcNow);

    private async Task<bool> RecipeExistsAsync(
        RecipeId recipeId,
        CancellationToken cancellationToken
    ) => await recipeRepository.ExistsAsync(recipeId, cancellationToken);

    private static IReadOnlyList<Guid> ListRecipeIds(RecipeCollection collection) =>
        collection.Items.Select(i => i.RecipeId.Value).ToList();
}
