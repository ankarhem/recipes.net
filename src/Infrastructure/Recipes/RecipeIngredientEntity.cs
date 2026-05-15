namespace Infrastructure.Recipes;

public sealed class RecipeIngredientEntity
{
    public Guid Id { get; set; }
    public required Guid RecipeId { get; set; }
    public required string Text { get; set; }
    public RecipeEntity Recipe { get; set; } = null!;
}