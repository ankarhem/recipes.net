using System.Text.Json;
using AngleSharp;
using AngleSharp.Html.Dom;
using App.Crawler;
using Microsoft.Extensions.Logging;
using Schema.NET;

namespace Infrastructure.Crawler;

public sealed class ScraperService(ILogger<ScraperService> logger) : IScraperService
{
    public async Task<IReadOnlyList<Uri>> ExtractLinksAsync(
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

        var links = document
            .Links.OfType<IHtmlAnchorElement>()
            .Where(a => !string.IsNullOrWhiteSpace(a.Href))
            .Select(a =>
            {
                Uri.TryCreate(a.Href, UriKind.Absolute, out var uri);
                return uri;
            })
            .Where(u => u is not null && u.Host == baseUrl.Host)
            .Cast<Uri>()
            .ToList();

        logger.LogDebug(
            "Extracted {LinkCount} same-site links from {BaseUrl}",
            links.Count,
            baseUrl
        );

        return links;
    }

    public ExtractedRecipe? ExtractRecipe(string html)
    {
        var config = Configuration.Default;
        using var context = BrowsingContext.New(config);
        var document = context.OpenAsync(req => req.Content(html)).GetAwaiter().GetResult();

        var scriptNodes = document.QuerySelectorAll("script[type='application/ld+json']");
        foreach (var script in scriptNodes)
        {
            var jsonLd = script.TextContent;
            var result = TryDeserializeRecipe(jsonLd);
            if (result is not null)
            {
                logger.LogDebug("Extracted recipe from {Url}", document.Url);
                return result;
            }
        }

        logger.LogDebug("No Schema.org Recipe found in HTML");
        return null;
    }

    private static ExtractedRecipe? TryDeserializeRecipe(string jsonLd)
    {
        try
        {
            using var doc = JsonDocument.Parse(jsonLd);
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in root.EnumerateArray())
                {
                    var result = TryDeserializeSingle(element);
                    if (result is not null)
                    {
                        return result;
                    }
                }

                return null;
            }

            return TryDeserializeSingle(root);
        }
        catch
        {
            return null;
        }
    }

    private static ExtractedRecipe? TryDeserializeSingle(JsonElement element)
    {
        if (!element.TryGetProperty("@type", out var typeElement) || !HasRecipeType(typeElement))
        {
            return null;
        }

        var rawJson = element.GetRawText();
        var recipe = SchemaSerializer.DeserializeObject<Recipe>(rawJson);
        return recipe is not null ? new ExtractedRecipe(recipe, rawJson) : null;
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
