namespace Domain.Recipes;

public sealed record RecipeIngredient
{
    public required string Text { get; init; }
}
