using Domain.Recipes;
using Web.Models;

namespace Web.Mapping;

public static class RecipeMapper
{
    public static GetRecipeResponse ToResponse(Recipe recipe) =>
        new()
        {
            Id = recipe.Id.Value,
            Name = recipe.DisplayName,
            Description = recipe.Description,
            ImageUrls = recipe.ImageUrls,
            Category = recipe.Category,
            Cuisine = recipe.Cuisine,
            SuitableForDiets = recipe.SuitableForDiets.Select(d => d.ToString()).ToList(),
            PrepTime = recipe.PrepTime,
            CookTime = recipe.CookTime,
            TotalTime = recipe.TotalTime,
            ServingsCount = recipe.ServingsCount,
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
