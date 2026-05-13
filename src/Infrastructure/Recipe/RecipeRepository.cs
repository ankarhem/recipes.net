using App.Recipe;
using Microsoft.EntityFrameworkCore;
using DomainRecipe = Domain.Recipe.Recipe;

namespace Infrastructure.Recipe;

public sealed class RecipeRepository(RecipesDbContext db) : IRecipeRepository
{
    public async Task<Guid> SaveImportedAsync(
        DomainRecipe recipe,
        string sourceUrl,
        string rawSchemaJson,
        CancellationToken cancellationToken = default
    )
    {
        var existing = await db.Recipes.FirstOrDefaultAsync(
            r => r.Url == sourceUrl,
            cancellationToken
        );

        if (existing is not null)
        {
            return existing.Id;
        }

        var entity = RecipeEntity.FromImport(recipe, sourceUrl, rawSchemaJson);
        await db.Recipes.AddAsync(entity, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return entity.Id;
    }
}
