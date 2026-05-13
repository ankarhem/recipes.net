using Infrastructure.Recipe;
using Pgvector;

namespace Infrastructure.Embedding;

public sealed class RecipeEmbeddingEntity
{
    public Guid Id { get; set; }
    public Guid RecipeId { get; set; }
    public required string Model { get; set; }
    public int Dimensions { get; set; }
    public required string InputHash { get; set; }
    public Vector Embedding { get; set; } = null!;
    public RecipeEntity Recipe { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
