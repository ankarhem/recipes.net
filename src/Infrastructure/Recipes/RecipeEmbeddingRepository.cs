using App.Recipes;
using Domain;
using Domain.Recipes;
using Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Recipes;

public sealed class RecipeEmbeddingRepository(AppDbContext db, IClock clock) : IRecipeEmbeddingRepository
{
    public Task<bool> ExistsAsync(
        Guid recipeId,
        string model,
        int dimensions,
        string inputHash,
        CancellationToken cancellationToken = default
    )
    {
        var id = new RecipeId(recipeId);

        return db.RecipeEmbeddings.AnyAsync(
            e =>
                e.RecipeId == id
                && e.Model == model
                && e.Dimensions == dimensions
                && e.InputHash == inputHash,
            cancellationToken
        );
    }

    public async Task EnsureEmbeddingAsync(
        Guid recipeId,
        string model,
        int dimensions,
        string inputHash,
        ReadOnlyMemory<float> embedding,
        CancellationToken cancellationToken = default
    )
    {
        var id = new RecipeId(recipeId);

        var existing = await db.RecipeEmbeddings.FirstOrDefaultAsync(
            e =>
                e.RecipeId == id
                && e.Model == model
                && e.Dimensions == dimensions
                && e.InputHash == inputHash,
            cancellationToken
        );

        if (existing is not null)
        {
            return;
        }

        db.RecipeEmbeddings.Add(
            RecipeEmbedding.Create(id, model, dimensions, inputHash, embedding, clock)
        );
    }
}
