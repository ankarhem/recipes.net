
using Domain.Recipes;

namespace App.Recipes.Ports;

public interface IRecipeRepository
{
    Task<Recipe?> GetByIdAsync(RecipeId id, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(RecipeId id, CancellationToken cancellationToken = default);

    Task<RecipeId> SaveImportedAsync(
        Recipe recipe,
        string sourceUrl,
        string rawSchemaJson,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<Recipe>> SearchAsync(
        ReadOnlyMemory<float> queryEmbedding,
        EmbeddingModel model,
        int limit,
        CancellationToken cancellationToken = default
    );
}
