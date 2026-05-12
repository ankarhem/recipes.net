namespace Domain.Recipe;

public sealed record Recipe
{
    public string? Name { get; init; }
    public string? Description { get; init; }
    public required IReadOnlyList<string> ImageUrls { get; init; }
    public required IReadOnlyList<RecipeIngredient> Ingredients { get; init; }
    public required IReadOnlyList<RecipeInstruction> Instructions { get; init; }
}
