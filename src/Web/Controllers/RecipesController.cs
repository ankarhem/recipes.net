using App.Recipes;
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

        return Ok(RecipeMapper.ToResponse(recipe));
    }

    /// <summary>
    /// Search recipes by semantic similarity.
    /// </summary>
    /// <param name="query">The search query to find recipes by semantic similarity.</param>
    /// <param name="limit">Maximum number of results to return. Defaults to 10.</param>
    /// <response code="200">Returns matching recipes ordered by relevance.</response>
    /// <response code="400">The query is empty or whitespace.</response>
    [HttpGet("search")]
    [ProducesResponseType<SearchRecipesResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Search(
        [FromQuery] string query,
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Problem("Query must not be empty.", statusCode: 400);
        }

        limit = Math.Clamp(limit, 1, 100);

        var recipes = await recipeService.SearchRecipesAsync(query, limit, cancellationToken);

        return Ok(
            new SearchRecipesResponse
            {
                Query = query,
                Results = recipes.Select(RecipeMapper.ToResponse).ToList(),
            }
        );
    }
}