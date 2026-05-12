using Domain.Recipe;

namespace App.Crawler;

public interface IRecipeRepository
{
    Task SaveImportedAsync(
        Recipe recipe,
        string sourceUrl,
        string rawSchemaJson,
        CancellationToken cancellationToken = default
    );
}
