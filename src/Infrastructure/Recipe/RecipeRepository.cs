using App.Crawler;
using DomainRecipe = Domain.Recipe.Recipe;

namespace Infrastructure.Recipe;

public sealed class RecipeRepository(RecipesDbContext db) : IRecipeRepository
{
    public async Task SaveImportedAsync(
        DomainRecipe recipe,
        string sourceUrl,
        string rawSchemaJson,
        CancellationToken cancellationToken = default
    )
    {
        var entity = RecipeEntity.FromImport(recipe, sourceUrl, rawSchemaJson);
        await db.Recipes.AddAsync(entity, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }
}
