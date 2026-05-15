namespace Infrastructure.Recipes;

public sealed class RecipeInstructionEntity
{
    public Guid Id { get; set; }
    public required Guid RecipeId { get; set; }
    public required int Position { get; set; }
    public required string Text { get; set; }
    public string? Name { get; set; }
    public RecipeEntity Recipe { get; set; } = null!;
}