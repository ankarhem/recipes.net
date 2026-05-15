namespace App.Recipes;

public abstract record ToggleRecipeFavoriteResult
{
    public sealed record Success(bool IsFavorite) : ToggleRecipeFavoriteResult;
    public sealed record RecipeNotFound : ToggleRecipeFavoriteResult;
}
