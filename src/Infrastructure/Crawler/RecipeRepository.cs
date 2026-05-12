using App.Crawler;
using Domain.Recipe;

namespace Infrastructure.Crawler;

public sealed class RecipeRepository(RecipesDbContext db) : IRecipeRepository
{
    public async Task SaveImportedAsync(
        Recipe recipe,
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
