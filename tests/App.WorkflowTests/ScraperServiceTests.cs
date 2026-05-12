using AwesomeAssertions;
using Infrastructure.Crawler;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace App.WorkflowTests;

public class ScraperServiceTests
{
    private static readonly Uri BaseUrl = new("https://example.com/recipes/start");

    private static readonly string PageWithRecipeJsonLdHtml = """
        <html><head>
        <script type="application/ld+json">
        {
          "@context": "https://schema.org",
          "@type": "Recipe",
          "name": "Lemon Tart",
          "description": "A bright citrus dessert",
          "image": "https://example.com/images/lemon-tart.jpg",
          "recipeIngredient": ["2 lemons", "1 pie crust"],
          "recipeInstructions": ["Juice lemons", "Bake tart"]
        }
        </script>
        </head><body>recipe page</body></html>
        """;

    private static readonly string PageWithRecipeTypeArrayHtml = """
        <html><head>
        <script type="application/ld+json">
        {
          "@context": "https://schema.org",
          "@type": ["Recipe", "CreativeWork"],
          "name": "Banana Bread",
          "recipeIngredient": ["3 bananas"],
          "recipeInstructions": ["Mash bananas"]
        }
        </script>
        </head><body>recipe page</body></html>
        """;

    private static readonly string PageWithNonRecipeJsonLdHtml = """
        <html><head>
        <script type="application/ld+json">
        {
          "@context": "https://schema.org",
          "@type": "Article",
          "name": "Not a recipe"
        }
        </script>
        </head><body>article page</body></html>
        """;

    private static readonly string PageWithMissingAtTypeJsonLdHtml = """
        <html><head>
        <script type="application/ld+json">
        {
          "@context": "https://schema.org",
          "name": "Untyped content"
        }
        </script>
        </head><body>untyped page</body></html>
        """;

    private static readonly string PageWithMalformedJsonLdHtml = """
        <html><head>
        <script type="application/ld+json">
        { "@type": "Recipe", "name": "Broken Recipe",
        </script>
        </head><body>broken json page</body></html>
        """;

    private static readonly string PageWithMultipleScriptBlocksHtml = """
        <html><head>
        <script type="application/ld+json">
        {
          "@context": "https://schema.org",
          "@type": "Article",
          "name": "Intro Article"
        }
        </script>
        <script type="application/ld+json">
        {
          "@context": "https://schema.org",
          "@type": "Recipe",
          "name": "First Recipe",
          "recipeIngredient": ["1 cup oats"],
          "recipeInstructions": ["Toast oats"]
        }
        </script>
        <script type="application/ld+json">
        {
          "@context": "https://schema.org",
          "@type": "Recipe",
          "name": "Second Recipe",
          "recipeIngredient": ["1 cup flour"],
          "recipeInstructions": ["Mix flour"]
        }
        </script>
        </head><body>multiple scripts</body></html>
        """;

    private static readonly string PageWithoutScriptTagsHtml = """
        <html><head><title>No scripts</title></head><body>plain page</body></html>
        """;

    private static readonly string PageWithGraphContainingRecipeHtml = """
        <html><head>
        <script type="application/ld+json">
        {
          "@context": "https://schema.org",
          "@graph": [
            {
              "@type": "WebSite",
              "name": "Cooking Site",
              "url": "https://example.com"
            },
            {
              "@type": "Article",
              "headline": "How to bake"
            },
            {
              "@type": "Recipe",
              "name": "Graph Recipe",
              "recipeIngredient": ["1 cup sugar", "2 eggs"],
              "recipeInstructions": ["Mix well", "Bake until golden"]
            }
          ]
        }
        </script>
        </head><body>graph page</body></html>
        """;

    private static ScraperService CreateService() => new(NullLogger<ScraperService>.Instance);

    [Fact]
    public async Task ExtractPageAsync_PageWithRecipeJsonLd_ExtractsRecipe()
    {
        var service = CreateService();

        var result = await service.ExtractPageAsync(PageWithRecipeJsonLdHtml, BaseUrl);

        result.Recipe.Should().NotBeNull();
        result.Recipe!.Name.Should().Be("Lemon Tart");
        result.Recipe.Description.Should().Be("A bright citrus dessert");
        result.Recipe.ImageUrls.Should().ContainSingle("https://example.com/images/lemon-tart.jpg");
        result.Recipe.Ingredients.Should().HaveCount(2);
        result.Recipe.Instructions.Should().HaveCount(2);
        result.RawJsonLd.Should().NotBeNull();
        result.RawJsonLd.Should().Contain("\"@type\": \"Recipe\"");
    }

    [Fact]
    public async Task ExtractPageAsync_RecipeTypeAsArray_ExtractsRecipe()
    {
        var service = CreateService();

        var result = await service.ExtractPageAsync(PageWithRecipeTypeArrayHtml, BaseUrl);

        result.Recipe.Should().NotBeNull();
        result.Recipe!.Name.Should().Be("Banana Bread");
        result.RawJsonLd.Should().Contain("CreativeWork");
    }

    [Fact]
    public async Task ExtractPageAsync_NonRecipeType_ReturnsNoRecipe()
    {
        var service = CreateService();

        var result = await service.ExtractPageAsync(PageWithNonRecipeJsonLdHtml, BaseUrl);

        result.Recipe.Should().BeNull();
        result.RawJsonLd.Should().BeNull();
        result.Links.Should().BeEmpty();
    }

    [Fact]
    public async Task ExtractPageAsync_MissingAtType_ReturnsNoRecipe()
    {
        var service = CreateService();

        var result = await service.ExtractPageAsync(PageWithMissingAtTypeJsonLdHtml, BaseUrl);

        result.Recipe.Should().BeNull();
        result.RawJsonLd.Should().BeNull();
        result.Links.Should().BeEmpty();
    }

    [Fact]
    public async Task ExtractPageAsync_MalformedJsonLd_DoesNotThrow()
    {
        var service = CreateService();

        var result = await service.ExtractPageAsync(PageWithMalformedJsonLdHtml, BaseUrl);
        result.Recipe.Should().BeNull();
        result.RawJsonLd.Should().BeNull();
        result.Links.Should().BeEmpty();
    }

    [Fact]
    public async Task ExtractPageAsync_MultipleScriptBlocks_ReturnsFirstRecipe()
    {
        var service = CreateService();

        var result = await service.ExtractPageAsync(PageWithMultipleScriptBlocksHtml, BaseUrl);
        result.Recipe.Should().NotBeNull();
        result.Recipe!.Name.Should().Be("First Recipe");
        result.RawJsonLd.Should().Contain("First Recipe");
        result.RawJsonLd.Should().NotContain("Second Recipe");
    }

    [Fact]
    public async Task ExtractPageAsync_NoScriptTags_ReturnsEmpty()
    {
        var service = CreateService();

        var result = await service.ExtractPageAsync(PageWithoutScriptTagsHtml, BaseUrl);
        result.Recipe.Should().BeNull();
        result.RawJsonLd.Should().BeNull();
        result.Links.Should().BeEmpty();
    }

    [Fact]
    public async Task ExtractPageAsync_EmptyHtml_ReturnsEmpty()
    {
        var service = CreateService();

        var result = await service.ExtractPageAsync(string.Empty, BaseUrl);
        result.Recipe.Should().BeNull();
        result.RawJsonLd.Should().BeNull();
        result.Links.Should().BeEmpty();
    }

    [Fact]
    public async Task ExtractPageAsync_SameDomainLinks_AreExtracted()
    {
        var service = CreateService();
        var html = """
            <html><body>
            <a href="https://example.com/recipes/one">One</a>
            <a href="https://example.com/recipes/two?from=start">Two</a>
            </body></html>
            """;

        var result = await service.ExtractPageAsync(html, BaseUrl);
        result
            .Links.Should()
            .BeEquivalentTo([
                new Uri("https://example.com/recipes/one"),
                new Uri("https://example.com/recipes/two?from=start"),
            ]);
        result.Recipe.Should().BeNull();
        result.RawJsonLd.Should().BeNull();
    }

    [Fact]
    public async Task ExtractPageAsync_CrossDomainLinks_AreFiltered()
    {
        var service = CreateService();
        var html = """
            <html><body>
            <a href="https://example.com/recipes/internal">Internal</a>
            <a href="https://other.example.com/recipes/subdomain">Subdomain</a>
            <a href="https://other.com/recipes/external">External</a>
            </body></html>
            """;

        var result = await service.ExtractPageAsync(html, BaseUrl);
        result
            .Links.Should()
            .ContainSingle()
            .Which.Should()
            .Be(new Uri("https://example.com/recipes/internal"));
        result.Links.Should().NotContain(link => link.Host != BaseUrl.Host);
    }

    [Fact]
    public async Task ExtractPageAsync_RelativeUrls_AreResolved()
    {
        var service = CreateService();
        var html = """
            <html><body>
            <a href="/recipes/root-relative">Root relative</a>
            <a href="next-page">Path relative</a>
            </body></html>
            """;

        var result = await service.ExtractPageAsync(html, BaseUrl);
        result
            .Links.Should()
            .BeEquivalentTo([
                new Uri("https://example.com/recipes/root-relative"),
                new Uri("https://example.com/recipes/next-page"),
            ]);
    }

    [Theory]
    [InlineData("http://[::1")]
    [InlineData("http://")]
    public async Task ExtractPageAsync_InvalidHrefs_AreIgnored(string href)
    {
        var service = CreateService();
        var html = $"""
            <html><body>
            <a href="{href}">Invalid</a>
            </body></html>
            """;

        var result = await service.ExtractPageAsync(html, BaseUrl);
        result.Links.Should().BeEmpty();
        result.Recipe.Should().BeNull();
        result.RawJsonLd.Should().BeNull();
    }

    [Fact]
    public async Task ExtractPageAsync_BlankOrWhitespaceHrefs_AreIgnored()
    {
        var service = CreateService();
        var html = """
            <html><body>
            <a href="">Empty</a>
            <a href="   ">Whitespace</a>
            <a href="	">Tab</a>
            <a>No href attribute</a>
            </body></html>
            """;

        var result = await service.ExtractPageAsync(html, BaseUrl);
        result.Links.Should().BeEmpty();
    }

    [Fact]
    public async Task ExtractPageAsync_GraphContainingRecipe_ExtractsRecipe()
    {
        var service = CreateService();

        var result = await service.ExtractPageAsync(PageWithGraphContainingRecipeHtml, BaseUrl);

        result.Recipe.Should().NotBeNull();
        result.Recipe!.Name.Should().Be("Graph Recipe");
        result.Recipe.Ingredients.Should().HaveCount(2);
        result.Recipe.Instructions.Should().HaveCount(2);
        result.RawJsonLd.Should().NotBeNull();
        result.RawJsonLd.Should().Contain("Graph Recipe");
        result.RawJsonLd.Should().NotContain("WebSite");
        result.RawJsonLd.Should().NotContain("@graph");
    }

    [Fact]
    public async Task ExtractPageAsync_GraphWithoutRecipe_ReturnsNoRecipe()
    {
        var service = CreateService();
        var html = """
            <html><head>
            <script type="application/ld+json">
            {
              "@context": "https://schema.org",
              "@graph": [
                { "@type": "WebSite", "name": "Site" },
                { "@type": "Article", "headline": "Not a recipe" }
              ]
            }
            </script>
            </head><body>graph page</body></html>
            """;

        var result = await service.ExtractPageAsync(html, BaseUrl);

        result.Recipe.Should().BeNull();
        result.RawJsonLd.Should().BeNull();
    }

    [Fact]
    public async Task ExtractPageAsync_GraphWithMultipleRecipes_ReturnsFirst()
    {
        var service = CreateService();
        var html = """
            <html><head>
            <script type="application/ld+json">
            {
              "@context": "https://schema.org",
              "@graph": [
                {
                  "@type": "Recipe",
                  "name": "First Graph Recipe",
                  "recipeIngredient": ["1 cup"],
                  "recipeInstructions": ["Step 1"]
                },
                {
                  "@type": "Recipe",
                  "name": "Second Graph Recipe",
                  "recipeIngredient": ["2 cups"],
                  "recipeInstructions": ["Step 2"]
                }
              ]
            }
            </script>
            </head><body>graph page</body></html>
            """;

        var result = await service.ExtractPageAsync(html, BaseUrl);

        result.Recipe.Should().NotBeNull();
        result.Recipe!.Name.Should().Be("First Graph Recipe");
        result.RawJsonLd.Should().Contain("First Graph Recipe");
        result.RawJsonLd.Should().NotContain("Second Graph Recipe");
    }
}
