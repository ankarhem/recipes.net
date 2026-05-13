using DomainRecipe = Domain.Recipe.Recipe;

namespace App.Embedding;

public interface IEmbeddingService
{
    Task EnsureRecipeEmbeddingAsync(
        Guid recipeId,
        DomainRecipe recipe,
        EmbeddingModel model,
        CancellationToken cancellationToken = default
    );
}
