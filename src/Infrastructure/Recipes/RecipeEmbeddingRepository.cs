using App.Recipes.Ports;
using Domain;
using Domain.Recipes;
using Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Recipes;

public sealed class RecipeEmbeddingRepository(AppDbContext db, IClock clock) : IRecipeEmbeddingRepository
{
    public Task<bool> ExistsAsync(
        Guid recipeId,
        EmbeddingModel model,
        string inputHash,
        CancellationToken cancellationToken = default
    )
    {
        var id = new RecipeId(recipeId);

        return db.RecipeEmbeddings.AnyAsync(
            e =>
                e.RecipeId == id
                && e.Model == model.ProviderId
                && e.Dimensions == model.Dimensions
                && e.InputHash == inputHash,
            cancellationToken
        );
    }

    public async Task EnsureEmbeddingAsync(
        Guid recipeId,
        EmbeddingModel model,
        string inputHash,
        ReadOnlyMemory<float> embedding,
        CancellationToken cancellationToken = default
    )
    {
        var id = new RecipeId(recipeId);

        var existing = await db.RecipeEmbeddings.FirstOrDefaultAsync(
            e =>
                e.RecipeId == id
                && e.Model == model.ProviderId
                && e.Dimensions == model.Dimensions
                && e.InputHash == inputHash,
            cancellationToken
        );

        if (existing is not null)
        {
            return;
        }

        db.RecipeEmbeddings.Add(
            RecipeEmbedding.Create(id, model.ProviderId, model.Dimensions, inputHash, embedding, clock)
        );
    }
}
