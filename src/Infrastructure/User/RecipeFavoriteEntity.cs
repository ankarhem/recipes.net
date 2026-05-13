using Infrastructure.Recipe;
using DomainRecipeFavorite = Domain.User.RecipeFavorite;

namespace Infrastructure.User;

public sealed class RecipeFavoriteEntity
{
    public required Guid UserId { get; set; }
    public required Guid RecipeId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public UserEntity User { get; set; } = null!;
    public RecipeEntity Recipe { get; set; } = null!;

    public DomainRecipeFavorite ToDomain() =>
        new()
        {
            UserId = UserId,
            RecipeId = RecipeId,
            CreatedAt = CreatedAt,
        };
}
