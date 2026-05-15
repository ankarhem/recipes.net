namespace Domain.Recipes;

public sealed record RecipeInstruction
{
    public required int Position { get; init; }
    public required string Text { get; init; }
    public string? Name { get; init; }
}
