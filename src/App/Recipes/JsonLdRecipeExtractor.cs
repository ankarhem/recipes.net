using System.Text.Json;
using Domain.Recipes;
using Microsoft.Extensions.Logging;

namespace App.Recipes;

public sealed class JsonLdRecipeExtractor(ILogger<JsonLdRecipeExtractor> logger) : IRecipeExtractor
{
    private const int MaxCategoryCuisineLength = 200;

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
        var category = TrimToNull(schemaRecipe.RecipeCategory.FirstOrDefault());
        var cuisine = TrimToNull(schemaRecipe.RecipeCuisine.FirstOrDefault());
        var suitableForDiets = ExtractSuitableForDiets(schemaRecipe, element);
        var prepTime = schemaRecipe.PrepTime.FirstOrDefault();
        var cookTime = schemaRecipe.CookTime.FirstOrDefault();
        var totalTime = schemaRecipe.TotalTime.FirstOrDefault();
        var servingsCount = ExtractServingsCount(schemaRecipe, element);

        var recipe = Recipe.FromImport(
            name,
            description,
            imageUrls,
            ingredientTexts,
            instructionTexts,
            category,
            cuisine,
            suitableForDiets,
            prepTime,
            cookTime,
            totalTime,
            servingsCount
        );
        return new RecipeExtractionResult(recipe, rawJson);
    }

    private static string? TrimToNull(string? value)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }
        return trimmed.Length > MaxCategoryCuisineLength
            ? trimmed[..MaxCategoryCuisineLength]
            : trimmed;
    }

    private static IReadOnlyList<DietType> ExtractSuitableForDiets(
        Schema.NET.Recipe schemaRecipe,
        JsonElement element
    )
    {
        var diets = new List<DietType>();

        foreach (var schemaDiet in schemaRecipe.SuitableForDiet)
        {
            if (schemaDiet is { } diet && MapDietType(diet.ToString()) is { } mappedDiet)
            {
                diets.Add(mappedDiet);
            }
        }

        if (element.TryGetProperty("suitableForDiet", out var rawSuitableForDiet))
        {
            foreach (var value in EnumerateStringValues(rawSuitableForDiet))
            {
                if (MapDietType(value) is { } mappedDiet)
                {
                    diets.Add(mappedDiet);
                }
            }
        }

        return diets.Distinct().ToList();
    }

    private static int? ExtractServingsCount(
        Schema.NET.Recipe schemaRecipe,
        JsonElement element
    )
    {
        string? firstYieldText = null;

        foreach (var value in schemaRecipe.RecipeYield)
        {
            switch (value)
            {
                case int servings when servings > 0:
                    return servings;
                case string text when firstYieldText is null:
                    firstYieldText = text;
                    break;
                case Schema.NET.IQuantitativeValue quantitativeValue:
                    var quantitativeValueServings = ExtractServingsCount(quantitativeValue);
                    if (quantitativeValueServings is not null)
                    {
                        return quantitativeValueServings;
                    }
                    break;
            }
        }

        if (firstYieldText is not null)
        {
            return ParseLeadingInteger(firstYieldText);
        }

        return element.TryGetProperty("recipeYield", out var rawRecipeYield)
            ? ExtractServingsCount(rawRecipeYield)
            : null;
    }

    private static int? ExtractServingsCount(Schema.NET.IQuantitativeValue quantitativeValue)
    {
        string? firstValueText = null;

        foreach (var value in quantitativeValue.Value)
        {
            switch (value)
            {
                case int servings when servings > 0:
                    return servings;
                case double servings when double.IsInteger(servings) && servings > 0:
                    return (int)servings;
                case string text when firstValueText is null:
                    firstValueText = text;
                    break;
            }
        }

        return firstValueText is null ? null : ParseLeadingInteger(firstValueText);
    }

    private static int? ExtractServingsCount(JsonElement element)
    {
        if (
            element.ValueKind == JsonValueKind.Number
            && element.TryGetInt32(out var servings)
            && servings > 0
        )
        {
            return servings;
        }

        if (element.ValueKind == JsonValueKind.String)
        {
            return ParseLeadingInteger(element.GetString() ?? string.Empty);
        }

        if (element.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        string? firstYieldText = null;
        foreach (var item in element.EnumerateArray())
        {
            if (
                item.ValueKind == JsonValueKind.Number
                && item.TryGetInt32(out servings)
                && servings > 0
            )
            {
                return servings;
            }

            if (item.ValueKind == JsonValueKind.String && firstYieldText is null)
            {
                firstYieldText = item.GetString();
            }
        }

        return firstYieldText is null ? null : ParseLeadingInteger(firstYieldText);
    }

    private static int? ParseLeadingInteger(string value)
    {
        var trimmed = value.TrimStart();
        var digitCount = trimmed.TakeWhile(char.IsDigit).Count();
        if (digitCount == 0)
        {
            return null;
        }

        return int.TryParse(trimmed[..digitCount], out var servings) && servings > 0
            ? servings
            : null;
    }

    private static IEnumerable<string> EnumerateStringValues(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String && element.GetString() is { } value)
        {
            yield return value;
        }

        if (element.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String && item.GetString() is { } itemValue)
            {
                yield return itemValue;
            }
        }
    }

    private static DietType? MapDietType(string value)
    {
        var normalized = value.Trim().TrimEnd('/');
        var lastSlash = normalized.LastIndexOf('/');
        if (lastSlash >= 0)
        {
            normalized = normalized[(lastSlash + 1)..];
        }

        return normalized switch
        {
            "VegetarianDiet" => DietType.Vegetarian,
            "VeganDiet" => DietType.Vegan,
            "GlutenFreeDiet" => DietType.GlutenFree,
            "LowLactoseDiet" => DietType.LowLactose,
            "LowSaltDiet" => DietType.LowSalt,
            "LowFatDiet" => DietType.LowFat,
            "LowCalorieDiet" => DietType.LowCalorie,
            "DiabeticDiet" => DietType.Diabetic,
            "HalalDiet" => DietType.Halal,
            "KosherDiet" => DietType.Kosher,
            "HinduDiet" => DietType.Hindu,
            _ => null,
        };
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
