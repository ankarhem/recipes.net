using DomainRecipe = Domain.Recipe.Recipe;

namespace App.Recipe;

public interface IRecipeRepository
{
    Task SaveImportedAsync(
        DomainRecipe recipe,
        string sourceUrl,
        string rawSchemaJson,
        CancellationToken cancellationToken = default
    );
}
