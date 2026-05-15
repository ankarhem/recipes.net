using System.Text.Json;
using Domain.Recipes;
using Microsoft.Extensions.Logging;

namespace App.Recipes;

public sealed class JsonLdRecipeExtractor(ILogger<JsonLdRecipeExtractor> logger) : IRecipeExtractor
{
    public RecipeExtractionResult? TryExtract(IEnumerable<string> jsonLdScripts)
    {
        foreach (var script in jsonLdScripts)
        {
            var result = TryDeserializeRecipe(script);
            if (result is not null)
            {
                return result;
            }
        }

        return null;
    }

    private RecipeExtractionResult? TryDeserializeRecipe(string jsonLd)
    {
        try
        {
            using var doc = JsonDocument.Parse(jsonLd);
            return FindRecipe(doc.RootElement);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to parse JSON-LD script ({ScriptLength} chars)",
                jsonLd.Length
            );
            return null;
        }
    }

    private static RecipeExtractionResult? FindRecipe(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                var result = FindRecipe(item);
                if (result is not null)
                {
                    return result;
                }
            }
            return null;
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var direct = TryDeserializeSingle(element);
        if (direct is not null)
        {
            return direct;
        }

        if (element.TryGetProperty("@graph", out var graph))
        {
            return FindRecipe(graph);
        }

        return null;
    }

    private static RecipeExtractionResult? TryDeserializeSingle(JsonElement element)
    {
        if (!element.TryGetProperty("@type", out var typeElement) || !HasRecipeType(typeElement))
        {
            return null;
        }

        var rawJson = element.GetRawText();
        var schemaRecipe = Schema.NET.SchemaSerializer.DeserializeObject<Schema.NET.Recipe>(rawJson);
        if (schemaRecipe is null)
        {
            return null;
        }

        var imageUrls = schemaRecipe
            .Image.Where(u => u is Uri)
            .Cast<Uri>()
            .Select(u => u.ToString())
            .ToList();
        var ingredientTexts = schemaRecipe.RecipeIngredient.ToList();
        var instructionTexts = schemaRecipe.RecipeInstructions.OfType<string>().ToList();
        var name = schemaRecipe.Name.FirstOrDefault();
        var description = schemaRecipe.Description.FirstOrDefault() as string;

        var recipe = Recipe.FromImport(
            name,
            description,
            imageUrls,
            ingredientTexts,
            instructionTexts
        );
        return new RecipeExtractionResult(recipe, rawJson);
    }

    private static bool HasRecipeType(JsonElement typeElement)
    {
        if (typeElement.ValueKind == JsonValueKind.String)
        {
            return typeElement.GetString() == "Recipe";
        }

        if (typeElement.ValueKind == JsonValueKind.Array)
        {
            return typeElement.EnumerateArray().Any(t => t.GetString() == "Recipe");
        }

        return false;
    }
}
