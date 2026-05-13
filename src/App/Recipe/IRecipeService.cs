using DomainRecipe = Domain.Recipe.Recipe;

namespace App.Recipe;

public interface IRecipeService
{
    Task<DomainRecipe?> GetRecipeAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DomainRecipe>> SearchRecipesAsync(
        string query,
        int limit = 10,
        CancellationToken cancellationToken = default
    );
}