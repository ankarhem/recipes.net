using System.Text.Json;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using App.Recipes;
using Microsoft.Extensions.Logging;
using Schema.NET;

namespace Infrastructure.Crawler;

public sealed class ScraperService(ILogger<ScraperService> logger) : IScraperService
{
    public async Task<ExtractedPage> ExtractPageAsync(
        string html,
        Uri baseUrl,
        CancellationToken cancellationToken = default
    )
    {
        var config = Configuration.Default;
        using var context = BrowsingContext.New(config);
        var document = await context.OpenAsync(
            req => req.Content(html).Address(baseUrl.ToString()),
            cancellationToken
        );

        var links = ExtractLinks(document, baseUrl);
        var (recipe, rawJsonLd) = ExtractRecipe(document);

        if (recipe is not null)
        {
            logger.LogDebug(
                "Extracted recipe and {LinkCount} links from {Url}",
                links.Count,
                document.Url
            );
        }
        else
        {
            logger.LogDebug(
                "Extracted {LinkCount} links from {Url} (no recipe)",
                links.Count,
                document.Url
            );
        }

        return new ExtractedPage
        {
            Links = links,
            Recipe = recipe,
            RawJsonLd = rawJsonLd,
        };
    }

    private static List<Uri> ExtractLinks(IDocument document, Uri baseUrl)
    {
        return document
            .Links.OfType<IHtmlAnchorElement>()
            .Where(a => !string.IsNullOrWhiteSpace(a.GetAttribute("href")))
            .Select(a =>
            {
                Uri.TryCreate(a.Href, UriKind.Absolute, out var uri);
                return uri;
            })
            .Where(u => u is not null && u.Host == baseUrl.Host)
            .Cast<Uri>()
            .ToList();
    }

    private (Domain.Recipes.Recipe? Recipe, string? RawJsonLd) ExtractRecipe(IDocument document)
    {
        var scriptNodes = document.QuerySelectorAll("script[type='application/ld+json']");
        foreach (var script in scriptNodes)
        {
            var result = TryDeserializeRecipe(script.TextContent);
            if (result is not null)
            {
                return result.Value;
            }
        }

        return (null, null);
    }

    private (Domain.Recipes.Recipe Recipe, string RawJsonLd)? TryDeserializeRecipe(string jsonLd)
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

    private static (Domain.Recipes.Recipe Recipe, string RawJsonLd)? FindRecipe(JsonElement element)
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

    private static (Domain.Recipes.Recipe Recipe, string RawJsonLd)? TryDeserializeSingle(
        JsonElement element
    )
    {
        if (!element.TryGetProperty("@type", out var typeElement) || !HasRecipeType(typeElement))
        {
            return null;
        }

        var rawJson = element.GetRawText();
        var schemaRecipe = SchemaSerializer.DeserializeObject<Schema.NET.Recipe>(rawJson);
        if (schemaRecipe is null)
        {
            return null;
        }

        var recipe = RecipeFactory.FromSchema(schemaRecipe);
        return (recipe, rawJson);
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
