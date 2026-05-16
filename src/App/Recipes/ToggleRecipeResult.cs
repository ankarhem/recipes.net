namespace App.Recipes;

public abstract record ToggleRecipeResult
{
    public sealed record Success(bool IsAdded) : ToggleRecipeResult;
    public sealed record RecipeNotFound : ToggleRecipeResult;
    public sealed record CollectionNotFound : ToggleRecipeResult;
}
