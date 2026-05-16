namespace App.Recipes;

public interface IRecipeCollectionService
{
    Task<ToggleRecipeResult> ToggleRecipeAsync(
        Guid collectionId,
        Guid recipeId,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<Guid>> ListRecipeIdsAsync(
        Guid collectionId,
        CancellationToken cancellationToken = default
    );

    Task<ToggleRecipeResult> ToggleFavoriteAsync(
        Guid userId,
        Guid recipeId,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<Guid>> ListFavoriteRecipeIdsAsync(
        Guid userId,
        CancellationToken cancellationToken = default
    );
}
