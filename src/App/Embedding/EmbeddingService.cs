using Domain.Recipes;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace App.Embedding;

public sealed class EmbeddingService(
    IRecipeEmbeddingTextBuilder textBuilder,
    IRecipeEmbeddingRepository repository,
    IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
    ILogger<EmbeddingService> logger
) : IEmbeddingService
{
    public async Task EnsureRecipeEmbeddingAsync(
        Guid recipeId,
        Recipe recipe,
        EmbeddingModel model,
        CancellationToken cancellationToken = default
    )
    {
        var dimensions = model.Dimensions();
        var modelId = model.OpenAiModelId();

        var canonicalText = textBuilder.Build(recipe);
        var inputHash = RecipeEmbeddingTextBuilder.ComputeInputHash(canonicalText);

        var alreadyExists = await repository.ExistsAsync(
            recipeId,
            modelId,
            dimensions,
            inputHash,
            cancellationToken
        );

        if (alreadyExists)
        {
            logger.LogDebug("Embedding already exists for recipe {RecipeId}, skipping", recipeId);
            return;
        }

        var result = await embeddingGenerator.GenerateAsync(
            [canonicalText],
            new EmbeddingGenerationOptions { Dimensions = dimensions },
            cancellationToken
        );

        var embedding = result.First();

        await repository.EnsureEmbeddingAsync(
            recipeId,
            modelId,
            dimensions,
            inputHash,
            embedding.Vector,
            cancellationToken
        );

        logger.LogInformation(
            "Generated embedding for recipe {RecipeId} using {Model} ({Dimensions}d)",
            recipeId,
            modelId,
            dimensions
        );
    }
}
