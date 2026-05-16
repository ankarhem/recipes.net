using App.Recipes.Ports;
using Domain.Recipes;
using Microsoft.Extensions.AI;

namespace Infrastructure.Recipes;

public sealed class RecipeSearchEmbeddingGenerator(
    IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator
) : IRecipeSearchEmbeddingGenerator
{
    public async Task<RecipeSearchEmbedding> GenerateAsync(
        string query,
        CancellationToken cancellationToken = default
    )
    {
        var model = EmbeddingModel.TextEmbedding3Small.Instance;

        var result = await embeddingGenerator.GenerateAsync(
            [query],
            new EmbeddingGenerationOptions { Dimensions = model.Dimensions },
            cancellationToken
        );

        var embedding = result.First();

        return new RecipeSearchEmbedding(embedding.Vector, model);
    }
}
