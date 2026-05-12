using Domain.Recipe;
using SchemaRecipe = Schema.NET.Recipe;

namespace App.Crawler;

public static class RecipeFactory
{
    public static Recipe FromSchema(SchemaRecipe schemaRecipe)
    {
        var imageUrls = schemaRecipe
            .Image.Where(u => u is Uri)
            .Cast<Uri>()
            .Select(u => u.ToString())
            .ToList();

        var ingredients = schemaRecipe
            .RecipeIngredient.Select(i => new RecipeIngredient(i))
            .ToList();

        var instructions = schemaRecipe
            .RecipeInstructions.Where(i => i is string)
            .Cast<string>()
            .Select((text, index) => new RecipeInstruction(index + 1, text))
            .ToList();

        return new Recipe(
            Name: schemaRecipe.Name.FirstOrDefault(),
            Description: schemaRecipe.Description.FirstOrDefault() as string,
            ImageUrls: imageUrls,
            Ingredients: ingredients,
            Instructions: instructions
        );
    }
}
