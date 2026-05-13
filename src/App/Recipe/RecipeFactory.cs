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
            .RecipeIngredient.Select(i => new DomainRecipeIngredient { Text = i })
            .ToList();

        var instructions = schemaRecipe
            .RecipeInstructions.Where(i => i is string)
            .Cast<string>()
            .Select(
                (text, index) => new DomainRecipeInstruction { Position = index + 1, Text = text }
            )
            .ToList();

        return new DomainRecipe
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
