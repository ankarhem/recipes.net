namespace App.Recipes;

public interface IRecipeFavoriteService
{
    Task<ToggleRecipeFavoriteResult> ToggleFavoriteAsync(
        Guid userId,
        Guid recipeId,
        CancellationToken cancellationToken = default
    );
    Task<IReadOnlyList<Guid>> ListFavoriteRecipeIdsAsync(
        Guid userId,
        CancellationToken cancellationToken = default
    );
}
