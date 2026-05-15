using Domain.Recipes;

namespace App.Recipes;

public interface IRecipeExtractor
{
    RecipeExtractionResult? TryExtract(IEnumerable<string> jsonLdScripts);
}

public sealed record RecipeExtractionResult(Recipe Recipe, string RawJsonLd);
