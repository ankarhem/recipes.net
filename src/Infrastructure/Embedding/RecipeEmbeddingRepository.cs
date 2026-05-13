using App.Embedding;
using Infrastructure.Recipe;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pgvector;

namespace Infrastructure.Embedding;

public sealed class RecipeEmbeddingRepository(RecipesDbContext db) : IRecipeEmbeddingRepository
{
    private static readonly string UniqueViolation = "23505";

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

        var now = DateTimeOffset.UtcNow;
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

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is PostgresException { SqlState: var sqlState }
                && sqlState == UniqueViolation
            )
        {
            // Concurrent insert won the race — treat as success
        }
    }
}
