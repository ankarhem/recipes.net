using Domain.Identity;
using Domain.Recipes;
using Infrastructure.Recipes;

namespace Infrastructure.Recipes;

public sealed class RecipeFavoriteEntity
{
    public required UserId UserId { get; set; }
    public required Guid RecipeId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public RecipeEntity Recipe { get; set; } = null!;

    public RecipeFavorite ToDomain() =>
        new()
        {
            UserId = UserId.Value,
            RecipeId = RecipeId,
            CreatedAt = CreatedAt,
        };
}
