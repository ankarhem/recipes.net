using Domain.Recipes;

namespace App.Recipes.Ports;

public interface IRecipeEmbeddingRepository
{
    Task<bool> ExistsAsync(
        Guid recipeId,
        EmbeddingModel model,
        string inputHash,
        CancellationToken cancellationToken = default
    );

    Task EnsureEmbeddingAsync(
        Guid recipeId,
        EmbeddingModel model,
        string inputHash,
        ReadOnlyMemory<float> embedding,
        CancellationToken cancellationToken = default
    );
}
