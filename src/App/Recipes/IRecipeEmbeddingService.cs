
using Domain.Recipes;

namespace App.Recipes;

public interface IRecipeEmbeddingService
{
    Task EnsureRecipeEmbeddingAsync(
        Guid recipeId,
        Recipe recipe,
        RecipeEmbeddingModel model,
        CancellationToken cancellationToken = default
    );
}
