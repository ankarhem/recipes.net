using DomainRecipe = Domain.Recipe.Recipe;
using DomainRecipeIngredient = Domain.Recipe.RecipeIngredient;
using DomainRecipeInstruction = Domain.Recipe.RecipeInstruction;
using SchemaRecipe = Schema.NET.Recipe;

namespace App.Recipe;

public static class RecipeFactory
{
    public static DomainRecipe FromSchema(SchemaRecipe schemaRecipe)
    {
        var imageUrls = schemaRecipe
            .Image.Where(u => u is Uri)
            .Cast<Uri>()
            .Select(u => u.ToString())
            .ToList();

        var ingredients = schemaRecipe
            .RecipeIngredient.Select(i => new DomainRecipeIngredient(i))
            .ToList();

        var instructions = schemaRecipe
            .RecipeInstructions.Where(i => i is string)
            .Cast<string>()
            .Select((text, index) => new DomainRecipeInstruction(index + 1, text))
            .ToList();

        return new DomainRecipe(
            Name: schemaRecipe.Name.FirstOrDefault(),
            Description: schemaRecipe.Description.FirstOrDefault() as string,
            ImageUrls: imageUrls,
            Ingredients: ingredients,
            Instructions: instructions
        );
    }
}
