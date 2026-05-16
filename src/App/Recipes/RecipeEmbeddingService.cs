using App.Recipes.Ports;
using Domain.Recipes;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace App.Recipes;

public sealed class RecipeEmbeddingService(
    IRecipeEmbeddingTextBuilder textBuilder,
    IRecipeEmbeddingRepository repository,
    IUnitOfWork unitOfWork,
    IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
    ILogger<RecipeEmbeddingService> logger
) : IRecipeEmbeddingService
{
    public async Task EnsureRecipeEmbeddingAsync(
        Guid recipeId,
        Recipe recipe,
        EmbeddingModel model,
        CancellationToken cancellationToken = default
    )
    {
        var canonicalText = textBuilder.Build(recipe);
        var inputHash = RecipeEmbeddingTextBuilder.ComputeInputHash(canonicalText);

        var alreadyExists = await repository.ExistsAsync(
            recipeId,
            model,
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
            new EmbeddingGenerationOptions { Dimensions = model.Dimensions },
            cancellationToken
        );

        var embedding = result.First();

        await repository.EnsureEmbeddingAsync(
            recipeId,
            model,
            inputHash,
            embedding.Vector,
            cancellationToken
        );
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Generated embedding for recipe {RecipeId} using {Model} ({Dimensions}d)",
            recipeId,
            model.ProviderId,
            model.Dimensions
        );
    }
}
