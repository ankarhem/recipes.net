using App.Recipes;
using Domain;
using Microsoft.EntityFrameworkCore;
using Pgvector;

namespace Infrastructure.Recipes;

public sealed class RecipeEmbeddingRepository(RecipesDbContext db, IClock clock) : IRecipeEmbeddingRepository
{
    public Task<bool> ExistsAsync(
        Guid recipeId,
        string model,
        int dimensions,
        string inputHash,
        CancellationToken cancellationToken = default
    ) =>
        db.RecipeEmbeddings.AnyAsync(
            e =>
                e.RecipeId == recipeId
                && e.Model == model
                && e.Dimensions == dimensions
                && e.InputHash == inputHash,
            cancellationToken
        );

    public async Task EnsureEmbeddingAsync(
        Guid recipeId,
        string model,
        int dimensions,
        string inputHash,
        ReadOnlyMemory<float> embedding,
        CancellationToken cancellationToken = default
    )
    {
        var existing = await db.RecipeEmbeddings.FirstOrDefaultAsync(
            e =>
                e.RecipeId == recipeId
                && e.Model == model
                && e.Dimensions == dimensions
                && e.InputHash == inputHash,
            cancellationToken
        );

        if (existing is not null)
        {
            return;
        }

        var now = clock.UtcNow;
        var entity = new RecipeEmbeddingEntity
        {
            Id = Guid.NewGuid(),
            RecipeId = recipeId,
            Model = model,
            Dimensions = dimensions,
            InputHash = inputHash,
            Embedding = new Vector(embedding),
            CreatedAt = now,
            UpdatedAt = now,
        };

        db.RecipeEmbeddings.Add(entity);
    }
}
