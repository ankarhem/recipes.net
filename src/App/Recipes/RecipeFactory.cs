
using Domain.Recipes;

namespace App.Recipes;

public static class RecipeFactory
{
    public static Recipe FromSchema(Schema.NET.Recipe schemaRecipe)
    {
        var imageUrls = schemaRecipe
            .Image.Where(u => u is Uri)
            .Cast<Uri>()
            .Select(u => u.ToString())
            .ToList();

        var ingredients = schemaRecipe
            .RecipeIngredient.Select(i => new RecipeIngredient { Text = i })
            .ToList();

        var instructions = schemaRecipe
            .RecipeInstructions.Where(i => i is string)
            .Cast<string>()
            .Select(
                (text, index) => new RecipeInstruction { Position = index + 1, Text = text }
            )
            .ToList();

        return new Recipe
        {
            Id = Guid.NewGuid(),
            Name = schemaRecipe.Name.FirstOrDefault(),
            Description = schemaRecipe.Description.FirstOrDefault() as string,
            ImageUrls = imageUrls,
            Ingredients = ingredients,
            Instructions = instructions,
        };
    }
}
