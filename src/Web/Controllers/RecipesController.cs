using App.Recipe;
using Microsoft.AspNetCore.Mvc;
using Web.Mapping;
using Web.Models;

namespace Web.Controllers;

[ApiController]
[Route("/api/v1/recipes")]
[Tags("Recipes")]
public class RecipesController(IRecipeService recipeService) : ControllerBase
{
    /// <summary>
    /// Get a recipe by ID.
    /// </summary>
    /// <response code="200">Returns the recipe.</response>
    /// <response code="404">Recipe not found.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<GetRecipeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var recipe = await recipeService.GetRecipeAsync(id, cancellationToken);

        if (recipe is null)
        {
            return NotFound();
        }

        return Ok(RecipeMapper.ToResponse(id, recipe));
    }
}