namespace App.Recipes;

public interface IRecipeEmbeddingRepository
{
    Task<bool> ExistsAsync(
        Guid recipeId,
        string model,
        int dimensions,
        string inputHash,
        CancellationToken cancellationToken = default
    );

    Task EnsureEmbeddingAsync(
        Guid recipeId,
        string model,
        int dimensions,
        string inputHash,
        ReadOnlyMemory<float> embedding,
        CancellationToken cancellationToken = default
    );
}
