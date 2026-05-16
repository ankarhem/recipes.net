namespace Domain.Recipes;

public sealed class RecipeEmbedding
{
    public Guid Id { get; private set; }
    public RecipeId RecipeId { get; private set; }
    public string Model { get; private set; } = null!;
    public int Dimensions { get; private set; }
    public string InputHash { get; private set; } = null!;
    public ReadOnlyMemory<float> Embedding { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private RecipeEmbedding() { }

    public static RecipeEmbedding Create(
        RecipeId recipeId,
        string model,
        int dimensions,
        string inputHash,
        ReadOnlyMemory<float> embedding,
        IClock clock
    )
    {
        var now = clock.UtcNow;

        return new()
        {
            Id = Guid.NewGuid(),
            RecipeId = recipeId,
            Model = model,
            Dimensions = dimensions,
            InputHash = inputHash,
            Embedding = embedding,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }
}
