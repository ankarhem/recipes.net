using DomainRecipe = Domain.Recipe.Recipe;
using Web.Models;

namespace Web.Mapping;

public static class RecipeMapper
{
    public static GetRecipeResponse ToResponse(Guid id, DomainRecipe recipe) =>
        new()
        {
            Id = id,
            Name = recipe.Name ?? "Untitled",
            Description = recipe.Description,
            ImageUrls = recipe.ImageUrls,
            Ingredients = recipe.Ingredients
                .Select(i => new RecipeIngredientResponse { Text = i.Text })
                .ToList(),
            Instructions = recipe.Instructions
                .Select(i => new RecipeInstructionResponse
                {
                    Position = i.Position,
                    Text = i.Text,
                    Name = i.Name,
                })
                .ToList(),
        };
}