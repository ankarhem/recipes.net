
using Domain.Recipes;

namespace App.Recipes;

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
        string model,
        int dimensions,
        int limit,
        CancellationToken cancellationToken = default
    );
}
