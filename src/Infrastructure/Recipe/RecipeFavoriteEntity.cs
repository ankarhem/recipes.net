using Infrastructure.Recipe;
using Domain.Identity;
using DomainRecipeFavorite = Domain.Recipe.RecipeFavorite;

namespace Infrastructure.Recipe;

public sealed class RecipeFavoriteEntity
{
    public required UserId UserId { get; set; }
    public required Guid RecipeId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public RecipeEntity Recipe { get; set; } = null!;

    public DomainRecipeFavorite ToDomain() =>
        new()
        {
            UserId = UserId.Value,
            RecipeId = RecipeId,
            CreatedAt = CreatedAt,
        };
}
