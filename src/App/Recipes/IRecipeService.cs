
using Domain.Recipes;

namespace App.Recipes;

public interface IRecipeService
{
    Task<Recipe?> GetRecipeAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Recipe>> SearchRecipesAsync(
        string query,
        int limit = 10,
        CancellationToken cancellationToken = default
    );
}