namespace Domain.Recipe;

public sealed record RecipeFavorite
{
    public required Guid UserId { get; init; }
    public required Guid RecipeId { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}
