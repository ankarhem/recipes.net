namespace Domain.Recipes;

public sealed record Recipe
{
    public static Recipe FromImport(
        string? name,
        string? description,
        IReadOnlyList<string> imageUrls,
        IReadOnlyList<string> ingredientTexts,
        IReadOnlyList<string> instructionTexts
    )
    {
        return new Recipe
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            ImageUrls = imageUrls,
            Ingredients = ingredientTexts.Select(t => new RecipeIngredient { Text = t }).ToList(),
            Instructions = instructionTexts
                .Select((t, i) => new RecipeInstruction { Position = i + 1, Text = t })
                .ToList(),
        };
    }

    public required Guid Id { get; init; }
    public string? Name { get; init; }
    public string? Description { get; init; }
    public required IReadOnlyList<string> ImageUrls { get; init; }
    public required IReadOnlyList<RecipeIngredient> Ingredients { get; init; }
    public required IReadOnlyList<RecipeInstruction> Instructions { get; init; }
}
