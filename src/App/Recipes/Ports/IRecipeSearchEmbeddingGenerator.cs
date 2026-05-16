namespace App.Recipes.Ports;

public readonly record struct RecipeSearchEmbedding(
    ReadOnlyMemory<float> Vector,
    string Model,
    int Dimensions
);

public interface IRecipeSearchEmbeddingGenerator
{
    Task<RecipeSearchEmbedding> GenerateAsync(
        string query,
        CancellationToken cancellationToken = default
    );
}
