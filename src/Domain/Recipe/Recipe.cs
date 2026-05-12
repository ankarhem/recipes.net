namespace Domain.Recipe;

public sealed record Recipe(
    string? Name,
    string? Description,
    IReadOnlyList<string> ImageUrls,
    IReadOnlyList<RecipeIngredient> Ingredients,
    IReadOnlyList<RecipeInstruction> Instructions
);
