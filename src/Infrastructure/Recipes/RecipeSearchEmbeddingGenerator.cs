using App.Recipes;
using App.Recipes.Ports;
using Microsoft.Extensions.AI;

namespace Infrastructure.Recipes;

public sealed class RecipeSearchEmbeddingGenerator(
    IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator
) : IRecipeSearchEmbeddingGenerator
{
    private const string Model = "text-embedding-3-small";
    private const int Dimensions = 1536;

    public async Task<RecipeSearchEmbedding> GenerateAsync(
        string query,
        CancellationToken cancellationToken = default
    )
    {
        var result = await embeddingGenerator.GenerateAsync(
            [query],
            new EmbeddingGenerationOptions { Dimensions = Dimensions },
            cancellationToken
        );

        var embedding = result.First();

        return new RecipeSearchEmbedding(embedding.Vector, Model, Dimensions);
    }
}
