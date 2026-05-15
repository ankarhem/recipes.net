
using Domain.Recipes;

namespace App.Recipes;

public interface IRecipeRepository
{
    Task<Recipe?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Guid> SaveImportedAsync(
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
