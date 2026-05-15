using App.Recipes;
using AwesomeAssertions;
using Domain.Recipes;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace App.UnitTests;

public class JsonLdRecipeExtractorTests
{
    [Fact]
    public void TryExtract_WithRecipeJsonLd_ReturnsRecipeAndRawJsonLd()
    {
        var extractor = CreateExtractor();
        var jsonLd = """
            {
              "@context": "https://schema.org",
              "@type": "Recipe",
              "name": "Lemon Tart",
              "description": "A bright citrus dessert",
              "image": "https://example.com/images/lemon-tart.jpg",
              "recipeIngredient": ["2 lemons", "1 pie crust"],
              "recipeInstructions": ["Juice lemons", "Bake tart"]
            }
            """;

        var result = extractor.TryExtract([jsonLd]);

        result.Should().NotBeNull();
        result!.RawJsonLd.Should().Contain("Lemon Tart");
        result.Recipe.Name.Should().Be("Lemon Tart");
        result.Recipe.Description.Should().Be("A bright citrus dessert");
        result.Recipe.ImageUrls.Should().ContainSingle("https://example.com/images/lemon-tart.jpg");
        result
            .Recipe.Ingredients.Should()
            .Equal(
                new RecipeIngredient { Text = "2 lemons" },
                new RecipeIngredient { Text = "1 pie crust" }
            );
        result
            .Recipe.Instructions.Should()
            .Equal(
                new RecipeInstruction { Position = 1, Text = "Juice lemons" },
                new RecipeInstruction { Position = 2, Text = "Bake tart" }
            );
    }

    [Fact]
    public void TryExtract_WithNoRecipeInScripts_ReturnsNull()
    {
        var extractor = CreateExtractor();

        var result = extractor.TryExtract([
            """
            { "@context": "https://schema.org", "@type": "Article", "name": "Not a recipe" }
            """,
            """
            { "@context": "https://schema.org", "name": "Untyped content" }
            """,
        ]);

        result.Should().BeNull();
    }

    [Fact]
    public void TryExtract_WithGraphContainingRecipe_ReturnsNestedRecipe()
    {
        var extractor = CreateExtractor();
        var jsonLd = """
            {
              "@context": "https://schema.org",
              "@graph": [
                { "@type": "WebSite", "name": "Cooking Site" },
                {
                  "@type": "Recipe",
                  "name": "Graph Recipe",
                  "recipeIngredient": ["1 cup sugar", "2 eggs"],
                  "recipeInstructions": ["Mix well", "Bake until golden"]
                }
              ]
            }
            """;

        var result = extractor.TryExtract([jsonLd]);

        result.Should().NotBeNull();
        result!.Recipe.Name.Should().Be("Graph Recipe");
        result.RawJsonLd.Should().Contain("Graph Recipe");
        result.RawJsonLd.Should().NotContain("WebSite");
        result.RawJsonLd.Should().NotContain("@graph");
    }

    [Fact]
    public void TryExtract_WithMalformedJsonLd_ReturnsNull()
    {
        var extractor = CreateExtractor();

        var result = extractor.TryExtract(["""{ "@type": "Recipe", "name": "Broken Recipe", """]);

        result.Should().BeNull();
    }

    [Fact]
    public void TryExtract_WithMalformedThenValidJsonLd_ReturnsValidRecipe()
    {
        var extractor = CreateExtractor();
        var validRecipe = """
            {
              "@context": "https://schema.org",
              "@type": "Recipe",
              "name": "Fallback Recipe"
            }
            """;

        var result = extractor.TryExtract([
            """{ "@type": "Recipe", "name": "Broken Recipe", """,
            validRecipe,
        ]);

        result.Should().NotBeNull();
        result!.Recipe.Name.Should().Be("Fallback Recipe");
    }

    [Fact]
    public void TryExtract_WithRecipeTypeArray_ReturnsRecipe()
    {
        var extractor = CreateExtractor();
        var jsonLd = """
            {
              "@context": "https://schema.org",
              "@type": ["Recipe", "CreativeWork"],
              "name": "Banana Bread"
            }
            """;

        var result = extractor.TryExtract([jsonLd]);

        result.Should().NotBeNull();
        result!.Recipe.Name.Should().Be("Banana Bread");
        result.RawJsonLd.Should().Contain("CreativeWork");
    }

    [Fact]
    public void TryExtract_WithMultipleRecipes_ReturnsFirstRecipe()
    {
        var extractor = CreateExtractor();

        var result = extractor.TryExtract([
            """
            { "@context": "https://schema.org", "@type": "Recipe", "name": "First Recipe" }
            """,
            """
            { "@context": "https://schema.org", "@type": "Recipe", "name": "Second Recipe" }
            """,
        ]);

        result.Should().NotBeNull();
        result!.Recipe.Name.Should().Be("First Recipe");
        result.RawJsonLd.Should().Contain("First Recipe");
        result.RawJsonLd.Should().NotContain("Second Recipe");
    }

    [Fact]
    public void TryExtract_WithMultiValueName_TakesFirstName()
    {
        var extractor = CreateExtractor();
        var jsonLd = """
            {
              "@context": "https://schema.org",
              "@type": "Recipe",
              "name": ["Primary Name", "Alternate Name"]
            }
            """;

        var result = extractor.TryExtract([jsonLd]);

        result.Should().NotBeNull();
        result!.Recipe.Name.Should().Be("Primary Name");
    }

    [Fact]
    public void TryExtract_WithStructuredDescription_ReturnsNullDescription()
    {
        var extractor = CreateExtractor();
        var jsonLd = """
            {
              "@context": "https://schema.org",
              "@type": "Recipe",
              "name": "Structured Description Recipe",
              "description": { "@type": "TextObject", "text": "Structured description" }
            }
            """;

        var result = extractor.TryExtract([jsonLd]);

        result.Should().NotBeNull();
        result!.Recipe.Description.Should().BeNull();
    }

    [Fact]
    public void TryExtract_WithImagesContainingStructuredObjects_FiltersToUriValues()
    {
        var extractor = CreateExtractor();
        var jsonLd = """
            {
              "@context": "https://schema.org",
              "@type": "Recipe",
              "name": "Image Recipe",
              "image": [
                { "@type": "ImageObject", "url": "https://example.com/images/object.jpg" },
                "https://example.com/images/direct.jpg"
              ]
            }
            """;

        var result = extractor.TryExtract([jsonLd]);

        result.Should().NotBeNull();
        result!.Recipe.ImageUrls.Should().ContainSingle("https://example.com/images/direct.jpg");
    }

    [Fact]
    public void TryExtract_WithStructuredInstructions_FiltersToStringInstructions()
    {
        var extractor = CreateExtractor();
        var jsonLd = """
            {
              "@context": "https://schema.org",
              "@type": "Recipe",
              "name": "Instruction Recipe",
              "recipeInstructions": [
                { "@type": "HowToStep", "text": "Structured prep" },
                "Mix batter",
                { "@type": "HowToStep", "text": "Structured bake" },
                "Bake cake"
              ]
            }
            """;

        var result = extractor.TryExtract([jsonLd]);

        result.Should().NotBeNull();
        result
            !.Recipe.Instructions.Should()
            .Equal(
                new RecipeInstruction { Position = 1, Text = "Mix batter" },
                new RecipeInstruction { Position = 2, Text = "Bake cake" }
            );
    }

    [Fact]
    public void TryExtract_WithMissingCollections_ReturnsEmptyDomainCollections()
    {
        var extractor = CreateExtractor();
        var jsonLd = """
            {
              "@context": "https://schema.org",
              "@type": "Recipe",
              "name": "Sparse Recipe"
            }
            """;

        var result = extractor.TryExtract([jsonLd]);

        result.Should().NotBeNull();
        result!.Recipe.ImageUrls.Should().BeEmpty();
        result.Recipe.Ingredients.Should().BeEmpty();
        result.Recipe.Instructions.Should().BeEmpty();
    }

    private static JsonLdRecipeExtractor CreateExtractor() =>
        new(NullLogger<JsonLdRecipeExtractor>.Instance);
}
