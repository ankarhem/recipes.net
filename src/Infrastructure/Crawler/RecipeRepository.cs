using App.Crawler;
using Domain;

namespace Infrastructure.Crawler;

public sealed class RecipeRepository(RecipesDbContext db) : IRecipeRepository
{
    public async Task SaveAsync(RecipeEntity recipe, CancellationToken cancellationToken = default)
    {
        await db.Recipes.AddAsync(recipe, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }
}
