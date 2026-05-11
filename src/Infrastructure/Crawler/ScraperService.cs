using AngleSharp;
using AngleSharp.Html.Dom;
using App.Crawler;
using Microsoft.Extensions.Logging;

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

    public string? ExtractRecipeJsonLd(string html)
    {
        var config = Configuration.Default;
        using var context = BrowsingContext.New(config);
        var document = context.OpenAsync(req => req.Content(html)).GetAwaiter().GetResult();

        var scriptNodes = document.QuerySelectorAll("script[type='application/ld+json']");
        foreach (var script in scriptNodes)
        {
            var jsonLd = script.TextContent;
            if (ContainsRecipeType(jsonLd))
            {
                logger.LogDebug("Found Schema.org Recipe JSON-LD in HTML");
                return jsonLd;
            }
        }

        logger.LogDebug("No Schema.org Recipe found in HTML");
        return null;
    }

    private static bool ContainsRecipeType(string jsonLd)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(jsonLd);
            var root = doc.RootElement;

            if (root.ValueKind == System.Text.Json.JsonValueKind.Array)
                return root.EnumerateArray().Any(HasRecipeType);

            return HasRecipeType(root);
        }
        catch
        {
            return false;
        }
    }

    private static bool HasRecipeType(System.Text.Json.JsonElement element)
    {
        if (!element.TryGetProperty("@type", out var typeElement))
            return false;

        if (typeElement.ValueKind == System.Text.Json.JsonValueKind.String)
            return typeElement.GetString() == "Recipe";

        if (typeElement.ValueKind == System.Text.Json.JsonValueKind.Array)
            return typeElement.EnumerateArray().Any(t => t.GetString() == "Recipe");

        return false;
    }
}
