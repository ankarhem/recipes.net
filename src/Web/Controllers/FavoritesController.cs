using App.Recipes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Web.Identity;
using Web.Models;

namespace Web.Controllers;

[ApiController]
[Route("/api/v1/favorites")]
[Tags("Favorites")]
[Authorize]
public class FavoritesController(IRecipeCollectionService collectionService) : ControllerBase
{
    /// <summary>
    /// Toggle a recipe as favorite.
    /// </summary>
    /// <response code="200">Favorite toggled.</response>
    /// <response code="401">Not authenticated.</response>
    /// <response code="404">Recipe not found.</response>
    [HttpPost("{recipeId:guid}/toggle")]
    [ProducesResponseType<ToggleFavoriteResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Toggle(Guid recipeId, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var result = await collectionService.ToggleFavoriteAsync(userId, recipeId, cancellationToken);

        return result switch
        {
            ToggleRecipeResult.Success s => Ok(
                new ToggleFavoriteResponse { RecipeId = recipeId, IsFavorite = s.IsAdded }
            ),
            ToggleRecipeResult.RecipeNotFound => NotFound(new { error = "Recipe not found." }),
            _ => StatusCode(StatusCodes.Status500InternalServerError),
        };
    }

    /// <summary>
    /// List the current user's favorite recipe IDs.
    /// </summary>
    /// <response code="200">List of favorite recipe IDs.</response>
    /// <response code="401">Not authenticated.</response>
    [HttpGet]
    [ProducesResponseType<ListFavoritesResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var recipeIds = await collectionService.ListFavoriteRecipeIdsAsync(userId, cancellationToken);

        return Ok(new ListFavoritesResponse { RecipeIds = recipeIds });
    }
}
