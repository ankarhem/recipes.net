
using Domain.Recipes;

namespace App.Embedding;

public interface IEmbeddingService
{
    Task EnsureRecipeEmbeddingAsync(
        Guid recipeId,
        Recipe recipe,
        EmbeddingModel model,
        CancellationToken cancellationToken = default
    );
}
