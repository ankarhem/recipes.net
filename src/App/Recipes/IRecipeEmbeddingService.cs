
using Domain.Recipes;

namespace App.Recipes;

public interface IRecipeEmbeddingService
{
    Task EnsureRecipeEmbeddingAsync(
        Guid recipeId,
        Recipe recipe,
        EmbeddingModel model,
        CancellationToken cancellationToken = default
    );
}
