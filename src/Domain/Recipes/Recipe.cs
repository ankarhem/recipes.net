namespace Domain.Recipes;

public sealed record Recipe
{
    public static Recipe FromImport(
        string? name,
        string? description,
        IReadOnlyList<string> imageUrls,
        IReadOnlyList<string> ingredientTexts,
        IReadOnlyList<string> instructionTexts,
        string? category,
        string? cuisine,
        IReadOnlyList<DietType> suitableForDiets,
        TimeSpan? prepTime,
        TimeSpan? cookTime,
        TimeSpan? totalTime,
        int? servingsCount
    )
    {
        if (prepTime.HasValue && prepTime.Value < TimeSpan.Zero)
        {
            throw new ArgumentException("Prep time must not be negative.", nameof(prepTime));
        }

        if (cookTime.HasValue && cookTime.Value < TimeSpan.Zero)
        {
            throw new ArgumentException("Cook time must not be negative.", nameof(cookTime));
        }

        if (totalTime.HasValue && totalTime.Value < TimeSpan.Zero)
        {
            throw new ArgumentException("Total time must not be negative.", nameof(totalTime));
        }

        if (servingsCount.HasValue && servingsCount.Value < 0)
        {
            throw new ArgumentException("Servings count must not be negative.", nameof(servingsCount));
        }

        return new Recipe
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            ImageUrls = imageUrls,
            Category = category,
            Cuisine = cuisine,
            SuitableForDiets = suitableForDiets,
            PrepTime = prepTime,
            CookTime = cookTime,
            TotalTime = totalTime,
            ServingsCount = servingsCount,
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
    public string? Category { get; init; }
    public string? Cuisine { get; init; }
    public required IReadOnlyList<DietType> SuitableForDiets { get; init; }
    public TimeSpan? PrepTime { get; init; }
    public TimeSpan? CookTime { get; init; }
    public TimeSpan? TotalTime { get; init; }
    public int? ServingsCount { get; init; }
    public required IReadOnlyList<RecipeIngredient> Ingredients { get; init; }
    public required IReadOnlyList<RecipeInstruction> Instructions { get; init; }
}
