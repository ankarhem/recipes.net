using System.ComponentModel;

namespace Web.Models;

public sealed record ToggleFavoriteResponse
{
    [Description("The recipe identifier.")]
    public required Guid RecipeId { get; init; }

    [Description("Whether the recipe is now a favorite.")]
    public required bool IsFavorite { get; init; }
}

public sealed record ListFavoritesResponse
{
    [Description("The list of favorited recipe IDs.")]
    public required IReadOnlyList<Guid> RecipeIds { get; init; }
}
