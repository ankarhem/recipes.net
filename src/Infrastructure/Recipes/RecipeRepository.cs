using App.Recipes;
using Domain;
using Domain.Recipes;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;
using Pgvector;

namespace Infrastructure.Recipes;

public sealed class RecipeRepository(RecipesDbContext db, IClock clock) : IRecipeRepository
{
    public async Task<Recipe?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default
    )
    {
        var entity = await db.Recipes.AsNoTracking()
            .AsSplitQuery()
            .Include(r => r.IngredientEntities)
            .Include(r => r.InstructionEntities)
            .SingleOrDefaultAsync(r => r.Id == id, cancellationToken);

        return entity?.ToDomain();
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await db.Recipes.AnyAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<Guid> SaveImportedAsync(
        Recipe recipe,
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

        var entity = RecipeEntity.FromImport(recipe, sourceUrl, rawSchemaJson, clock.UtcNow);
        await db.Recipes.AddAsync(entity, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return entity.Id;
    }

    public async Task<IReadOnlyList<Recipe>> SearchAsync(
        ReadOnlyMemory<float> queryEmbedding,
        string model,
        int dimensions,
        int limit,
        CancellationToken cancellationToken = default
    )
    {
        var queryVector = new Vector(queryEmbedding);

        var hitIds = await db.RecipeEmbeddings
            .Where(e => e.Model == model && e.Dimensions == dimensions)
            .OrderBy(e => e.Embedding.CosineDistance(queryVector))
            .Take(limit)
            .Select(e => e.RecipeId)
            .ToListAsync(cancellationToken);

        if (hitIds.Count == 0)
        {
            return [];
        }

        var recipes = await db.Recipes.AsNoTracking()
            .AsSplitQuery()
            .Include(r => r.IngredientEntities)
            .Include(r => r.InstructionEntities)
            .Where(r => hitIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, cancellationToken);

        return hitIds
            .Where(recipes.ContainsKey)
            .Select(id => recipes[id].ToDomain())
            .ToList();
    }
}