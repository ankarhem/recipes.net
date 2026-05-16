using App.Recipes.Ports;
using Domain.Recipes;

namespace App.Recipes;

public sealed class RecipeService(
    IRecipeRepository recipeRepository,
    IRecipeSearchEmbeddingGenerator searchEmbeddingGenerator
) : IRecipeService
{
    public Task<Recipe?> GetRecipeAsync(
        Guid id,
        CancellationToken cancellationToken = default
    ) =>
        recipeRepository.GetByIdAsync(new RecipeId(id), cancellationToken);

    public async Task<IReadOnlyList<Recipe>> SearchRecipesAsync(
        string query,
        int limit = 10,
        CancellationToken cancellationToken = default
    )
    {
        var embedding = await searchEmbeddingGenerator.GenerateAsync(query, cancellationToken);

        return await recipeRepository.SearchAsync(
            embedding.Vector,
            embedding.Model,
            limit,
            cancellationToken
        );
    }
}
