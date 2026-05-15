using App.Embedding;
using Domain.Recipes;
using Microsoft.Extensions.AI;

namespace App.Recipes;

public sealed class RecipeService(
    IRecipeRepository recipeRepository,
    IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator
) : IRecipeService
{
    public Task<Recipe?> GetRecipeAsync(
        Guid id,
        CancellationToken cancellationToken = default
    ) =>
        recipeRepository.GetByIdAsync(id, cancellationToken);

    public async Task<IReadOnlyList<Recipe>> SearchRecipesAsync(
        string query,
        int limit = 10,
        CancellationToken cancellationToken = default
    )
    {
        var result = await embeddingGenerator.GenerateAsync(
            [query],
            new EmbeddingGenerationOptions
            {
                Dimensions = EmbeddingModel.TextEmbedding3Small.Dimensions(),
            },
            cancellationToken
        );

        var embedding = result.First();

        return await recipeRepository.SearchAsync(
            embedding.Vector,
            EmbeddingModel.TextEmbedding3Small.OpenAiModelId(),
            EmbeddingModel.TextEmbedding3Small.Dimensions(),
            limit,
            cancellationToken
        );
    }
}