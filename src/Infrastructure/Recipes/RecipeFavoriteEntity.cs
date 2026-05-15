using Domain.Recipes;

namespace Infrastructure.Recipes;

public sealed class RecipeFavoriteEntity
{
    public required Guid UserId { get; set; }
    public required Guid RecipeId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public RecipeEntity Recipe { get; set; } = null!;

    public RecipeFavorite ToDomain() =>
        new()
        {
            UserId = UserId,
            RecipeId = RecipeId,
            CreatedAt = CreatedAt,
        };
}
