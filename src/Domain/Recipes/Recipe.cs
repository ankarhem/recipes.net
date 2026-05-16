using System.Text.Json.Serialization;

namespace Domain.Recipes;

public sealed class Recipe
{
    public const int MaxCategoryCuisineLength = 200;

    private readonly List<RecipeIngredient> _ingredients;
    private readonly List<RecipeInstruction> _instructions;

    private Recipe() { }

    private Recipe(
        RecipeId id,
        string? name,
        string? description,
        IEnumerable<string> imageUrls,
        string? category,
        string? cuisine,
        IEnumerable<DietType> suitableForDiets,
        TimeSpan? prepTime,
        TimeSpan? cookTime,
        TimeSpan? totalTime,
        int? servingsCount,
        IEnumerable<RecipeIngredient> ingredients,
        IEnumerable<RecipeInstruction> instructions
    )
    {
        Id = id;
        Name = name;
        Description = description;
        ImageUrls = imageUrls.ToList();
        Category = NormalizeFilteringValue(category, nameof(category));
        Cuisine = NormalizeFilteringValue(cuisine, nameof(cuisine));
        SuitableForDiets = suitableForDiets.Distinct().ToList();
        PrepTime = EnsureNonNegative(prepTime, nameof(prepTime), "Prep time");
        CookTime = EnsureNonNegative(cookTime, nameof(cookTime), "Cook time");
        TotalTime = EnsureNonNegative(totalTime, nameof(totalTime), "Total time");
        ServingsCount = EnsureNonNegative(servingsCount, nameof(servingsCount), "Servings count");
        _ingredients = ingredients.ToList();
        _instructions = NormalizeInstructions(instructions);
    }

    [JsonConstructor]
    internal Recipe(
        RecipeId id,
        string? name,
        string? description,
        IReadOnlyList<string> imageUrls,
        string? category,
        string? cuisine,
        IReadOnlyList<DietType> suitableForDiets,
        TimeSpan? prepTime,
        TimeSpan? cookTime,
        TimeSpan? totalTime,
        int? servingsCount,
        IReadOnlyList<RecipeIngredient> ingredients,
        IReadOnlyList<RecipeInstruction> instructions
    )
        : this(
            id,
            name,
            description,
            imageUrls,
            category,
            cuisine,
            suitableForDiets,
            prepTime,
            cookTime,
            totalTime,
            servingsCount,
            ingredients,
            instructions.AsEnumerable()
        ) { }

    public RecipeId Id { get; }
    public string? Name { get; private set; }
    [JsonIgnore]
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? "Untitled" : Name;
    public string? Description { get; private set; }
    public IReadOnlyList<string> ImageUrls { get; private set; }
    public string? Category { get; private set; }
    public string? Cuisine { get; private set; }
    public IReadOnlyList<DietType> SuitableForDiets { get; private set; }
    public TimeSpan? PrepTime { get; private set; }
    public TimeSpan? CookTime { get; private set; }
    public TimeSpan? TotalTime { get; private set; }
    public int? ServingsCount { get; private set; }
    public IReadOnlyList<RecipeIngredient> Ingredients => _ingredients;
    public IReadOnlyList<RecipeInstruction> Instructions => _instructions;

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
    ) =>
        new(
            RecipeId.New(),
            name,
            description,
            imageUrls,
            category,
            cuisine,
            suitableForDiets,
            prepTime,
            cookTime,
            totalTime,
            servingsCount,
            ingredientTexts.Select(t => new RecipeIngredient { Text = t }),
            instructionTexts.Select((t, i) => new RecipeInstruction { Position = i + 1, Text = t })
        );

    public static Recipe Rehydrate(
        RecipeId id,
        string? name,
        string? description,
        IReadOnlyList<string> imageUrls,
        string? category,
        string? cuisine,
        IReadOnlyList<DietType> suitableForDiets,
        TimeSpan? prepTime,
        TimeSpan? cookTime,
        TimeSpan? totalTime,
        int? servingsCount,
        IReadOnlyList<RecipeIngredient> ingredients,
        IReadOnlyList<RecipeInstruction> instructions
    ) =>
        new(
            id,
            name,
            description,
            imageUrls,
            category,
            cuisine,
            suitableForDiets,
            prepTime,
            cookTime,
            totalTime,
            servingsCount,
            ingredients,
            instructions
        );

    public void UpdateDetails(
        string? name,
        string? description,
        string? category,
        string? cuisine,
        int? servingsCount,
        TimeSpan? prepTime,
        TimeSpan? cookTime,
        TimeSpan? totalTime
    )
    {
        var normalizedCategory = NormalizeFilteringValue(category, nameof(category));
        var normalizedCuisine = NormalizeFilteringValue(cuisine, nameof(cuisine));
        var normalizedServingsCount = EnsureNonNegative(
            servingsCount,
            nameof(servingsCount),
            "Servings count"
        );
        var normalizedPrepTime = EnsureNonNegative(prepTime, nameof(prepTime), "Prep time");
        var normalizedCookTime = EnsureNonNegative(cookTime, nameof(cookTime), "Cook time");
        var normalizedTotalTime = EnsureNonNegative(totalTime, nameof(totalTime), "Total time");

        Name = name;
        Description = description;
        Category = normalizedCategory;
        Cuisine = normalizedCuisine;
        ServingsCount = normalizedServingsCount;
        PrepTime = normalizedPrepTime;
        CookTime = normalizedCookTime;
        TotalTime = normalizedTotalTime;
    }

    public void SetImageUrls(IReadOnlyList<string> imageUrls) => ImageUrls = imageUrls.ToList();

    public void SetSuitableForDiets(IReadOnlyList<DietType> suitableForDiets) =>
        SuitableForDiets = suitableForDiets.Distinct().ToList();

    public void ReplaceIngredients(IReadOnlyList<string> ingredientTexts)
    {
        _ingredients.Clear();
        _ingredients.AddRange(ingredientTexts.Select(t => new RecipeIngredient { Text = t }));
    }

    public void ReplaceInstructions(IReadOnlyList<string> instructionTexts)
    {
        _instructions.Clear();
        _instructions.AddRange(
            instructionTexts.Select((t, i) => new RecipeInstruction { Position = i + 1, Text = t })
        );
    }

    private static string? NormalizeFilteringValue(string? value, string paramName)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        return trimmed.Length > MaxCategoryCuisineLength
            ? trimmed[..MaxCategoryCuisineLength]
            : trimmed;
    }

    private static TimeSpan? EnsureNonNegative(TimeSpan? value, string paramName, string label)
    {
        if (value.HasValue && value.Value < TimeSpan.Zero)
        {
            throw new ArgumentException($"{label} must not be negative.", paramName);
        }

        return value;
    }

    private static int? EnsureNonNegative(int? value, string paramName, string label)
    {
        if (value.HasValue && value.Value < 0)
        {
            throw new ArgumentException($"{label} must not be negative.", paramName);
        }

        return value;
    }

    private static List<RecipeInstruction> NormalizeInstructions(
        IEnumerable<RecipeInstruction> instructions
    ) =>
        instructions
            .OrderBy(i => i.Position)
            .Select((i, index) => new RecipeInstruction
            {
                Position = index + 1,
                Text = i.Text,
                Name = i.Name,
            })
            .ToList();
}
