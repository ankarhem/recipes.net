namespace Domain.Recipe;

public sealed record RecipeIngredient
{
    public required string Text { get; init; }
}
