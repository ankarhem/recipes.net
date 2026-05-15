using Domain.Recipes;
using Microsoft.Extensions.Logging;
using Temporalio.Activities;
using Temporalio.Exceptions;

namespace App.Embedding;

public sealed class EmbeddingActivities(
    IEmbeddingService embeddingService,
    ILogger<EmbeddingActivities> logger
)
{
    [Activity]
    public async Task EnsureRecipeEmbeddingAsync(
        Guid recipeId,
        Recipe recipe,
        EmbeddingModel model
    )
    {
        var ct = ActivityExecutionContext.Current.CancellationToken;

        try
        {
            await embeddingService.EnsureRecipeEmbeddingAsync(
                recipeId,
                recipe,
                model,
                ct
            );
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Embedding generation failed for recipe {RecipeId}", recipeId);

            throw new ApplicationFailureException(
                $"Embedding generation failed for recipe {recipeId}: {ex.Message}",
                inner: ex,
                errorType: "embedding-failure",
                nonRetryable: false
            );
        }
    }
}
