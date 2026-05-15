using System.ComponentModel;

namespace Web.Models;

public sealed record GetRecipeResponse
{
    [Description("The unique identifier of the recipe.")]
    public required Guid Id { get; init; }

    [Description("The name of the recipe.")]
    public required string Name { get; init; }

    [Description("A description of the recipe.")]
    public string? Description { get; init; }

    [Description("Image URLs for the recipe.")]
    public required IReadOnlyList<string> ImageUrls { get; init; }

    [Description("The recipe category, such as dessert or entree.")]
    public string? Category { get; init; }

    [Description("The recipe cuisine, such as Italian or Thai.")]
    public string? Cuisine { get; init; }

    [Description("Dietary guidelines this recipe is suitable for.")]
    public required IReadOnlyList<string> SuitableForDiets { get; init; }

    [Description("The preparation time.")]
    public TimeSpan? PrepTime { get; init; }

    [Description("The cooking time.")]
    public TimeSpan? CookTime { get; init; }

    [Description("The total time.")]
    public TimeSpan? TotalTime { get; init; }

    [Description("The number of servings produced by the recipe.")]
    public int? ServingsCount { get; init; }

    [Description("The ingredients required for the recipe.")]
    public required IReadOnlyList<RecipeIngredientResponse> Ingredients { get; init; }

    [Description("The steps to make the recipe.")]
    public required IReadOnlyList<RecipeInstructionResponse> Instructions { get; init; }
}

public sealed record RecipeIngredientResponse
{
    [Description("The ingredient text.")]
    public required string Text { get; init; }
}

public sealed record RecipeInstructionResponse
{
    [Description("The position of this instruction in the recipe.")]
    public required int Position { get; init; }

    [Description("The instruction text.")]
    public required string Text { get; init; }

    [Description("An optional name for this instruction group.")]
    public string? Name { get; init; }
}

public sealed record SearchRecipesResponse
{
    [Description("The search query that was used.")]
    public required string Query { get; init; }

    [Description("The search results, ordered by relevance.")]
    public required IReadOnlyList<GetRecipeResponse> Results { get; init; }
}
