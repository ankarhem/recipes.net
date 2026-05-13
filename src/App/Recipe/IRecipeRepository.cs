using DomainRecipe = Domain.Recipe.Recipe;

namespace App.Recipe;

public interface IRecipeRepository
{
    Task<DomainRecipe?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Guid> SaveImportedAsync(
        DomainRecipe recipe,
        string sourceUrl,
        string rawSchemaJson,
        CancellationToken cancellationToken = default
    );
}
