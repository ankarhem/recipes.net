using App.Embedding;
using Microsoft.Extensions.AI;
using DomainRecipe = Domain.Recipe.Recipe;

namespace App.Recipe;

public sealed class RecipeService(
    IRecipeRepository recipeRepository,
    IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator
) : IRecipeService
{
    public Task<DomainRecipe?> GetRecipeAsync(
        Guid id,
        CancellationToken cancellationToken = default
    ) =>
        recipeRepository.GetByIdAsync(id, cancellationToken);

    public async Task<IReadOnlyList<DomainRecipe>> SearchRecipesAsync(
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