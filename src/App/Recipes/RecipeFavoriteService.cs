namespace App.Recipes;

public sealed class RecipeFavoriteService(
    IRecipeFavoriteRepository favoriteRepository,
    IRecipeRepository recipeRepository
) : IRecipeFavoriteService
{
    public async Task<ToggleRecipeFavoriteResult> ToggleFavoriteAsync(
        Guid userId,
        Guid recipeId,
        CancellationToken cancellationToken = default
    )
    {
        var exists = await recipeRepository.ExistsAsync(recipeId, cancellationToken);
        if (!exists)
        {
            return new ToggleRecipeFavoriteResult.RecipeNotFound();
        }

        var existing = await favoriteRepository.FindAsync(userId, recipeId, cancellationToken);
        if (existing is not null)
        {
            await favoriteRepository.RemoveAsync(userId, recipeId, cancellationToken);
            return new ToggleRecipeFavoriteResult.Success(false);
        }

        await favoriteRepository.AddAsync(userId, recipeId, cancellationToken);
        return new ToggleRecipeFavoriteResult.Success(true);
    }

    public async Task<IReadOnlyList<Guid>> ListFavoriteRecipeIdsAsync(
        Guid userId,
        CancellationToken cancellationToken = default
    )
    {
        return await favoriteRepository.ListByUserIdAsync(userId, cancellationToken);
    }
}
