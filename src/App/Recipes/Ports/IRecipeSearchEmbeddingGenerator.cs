using Domain.Recipes;

namespace App.Recipes.Ports;

public readonly record struct RecipeSearchEmbedding(
    ReadOnlyMemory<float> Vector,
    EmbeddingModel Model
);

public interface IRecipeSearchEmbeddingGenerator
{
    Task<RecipeSearchEmbedding> GenerateAsync(
        string query,
        CancellationToken cancellationToken = default
    );
}
