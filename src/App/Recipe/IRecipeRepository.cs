using DomainRecipe = Domain.Recipe.Recipe;

namespace App.Recipe;

public interface IRecipeRepository
{
    Task<Guid> SaveImportedAsync(
        DomainRecipe recipe,
        string sourceUrl,
        string rawSchemaJson,
        CancellationToken cancellationToken = default
    );
}
