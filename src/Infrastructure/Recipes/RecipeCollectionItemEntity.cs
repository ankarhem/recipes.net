namespace Infrastructure.Recipes;

public sealed class RecipeCollectionItemEntity
{
    public Guid Id { get; set; }
    public Guid CollectionId { get; set; }
    public Guid RecipeId { get; set; }
    public DateTimeOffset AddedAt { get; set; }
    public int Position { get; set; }

    public RecipeCollectionEntity Collection { get; set; } = null!;
    public RecipeEntity Recipe { get; set; } = null!;
}
