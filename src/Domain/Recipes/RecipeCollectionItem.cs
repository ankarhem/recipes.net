namespace Domain.Recipes;

public sealed record RecipeCollectionItem
{
    public required RecipeId RecipeId { get; init; }
    public required DateTimeOffset AddedAt { get; init; }
    public required int Position { get; init; }
}
