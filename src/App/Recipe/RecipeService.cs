using DomainRecipe = Domain.Recipe.Recipe;

namespace App.Recipe;

public sealed class RecipeService(IRecipeRepository recipeRepository) : IRecipeService
{
    public Task<DomainRecipe?> GetRecipeAsync(
        Guid id,
        CancellationToken cancellationToken = default
    ) =>
        recipeRepository.GetByIdAsync(id, cancellationToken);
}