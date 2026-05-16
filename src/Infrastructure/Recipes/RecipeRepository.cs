using App.Recipes;
using App.Recipes.Ports;
using Domain;
using Domain.Recipes;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using Pgvector;

namespace Infrastructure.Recipes;

public sealed class RecipeRepository(AppDbContext db, IClock clock) : IRecipeRepository
{
    public async Task<Recipe?> GetByIdAsync(
        RecipeId id,
        CancellationToken cancellationToken = default
    )
    {
        return await db.Recipes
            .Include(r => r.Ingredients)
            .Include(r => r.Instructions)
            .AsSplitQuery()
            .SingleOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<bool> ExistsAsync(RecipeId id, CancellationToken cancellationToken = default)
    {
        return await db.Recipes.AnyAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<RecipeId> SaveImportedAsync(
        Recipe recipe,
        string sourceUrl,
        string rawSchemaJson,
        CancellationToken cancellationToken = default
    )
    {
        var exists = await db.Recipes.AnyAsync(
            r => EF.Property<string>(r, "Url") == sourceUrl,
            cancellationToken
        );

        if (exists)
        {
            return await db.Recipes
                .Where(r => EF.Property<string>(r, "Url") == sourceUrl)
                .Select(r => r.Id)
                .SingleAsync(cancellationToken);
        }

        await db.Recipes.AddAsync(recipe, cancellationToken);

        var now = clock.UtcNow;
        var entry = db.Entry(recipe);
        entry.Property<string>("Url").CurrentValue = sourceUrl;
        entry.Property<string>("JsonLd").CurrentValue = rawSchemaJson;
        entry.Property<DateTimeOffset>("CreatedAt").CurrentValue = now;
        entry.Property<DateTimeOffset>("UpdatedAt").CurrentValue = now;

        return recipe.Id;
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
            .FromSqlInterpolated(
                $"""
                SELECT *
                FROM "RecipeEmbeddings"
                WHERE "Model" = {model} AND "Dimensions" = {dimensions}
                ORDER BY "Embedding" <=> {queryVector}
                LIMIT {limit}
                """
            )
            .AsNoTracking()
            .Select(e => e.RecipeId)
            .ToListAsync(cancellationToken);

        if (hitIds.Count == 0)
        {
            return [];
        }

        var recipes = await db.Recipes
            .AsNoTracking()
            .Include(r => r.Ingredients)
            .Include(r => r.Instructions)
            .AsSplitQuery()
            .Where(r => hitIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, cancellationToken);

        return hitIds
            .Where(recipes.ContainsKey)
            .Select(id => recipes[id])
            .ToList();
    }
}
