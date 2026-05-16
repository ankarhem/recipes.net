using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using App.Recipes;
using App.Recipes.Ports;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Crawler;

public sealed class ScraperService(ILogger<ScraperService> logger, IRecipeExtractor recipeExtractor)
    : IScraperService
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
        var extraction = ExtractRecipe(document);

        if (extraction is not null)
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
            Recipe = extraction?.Recipe,
            RawJsonLd = extraction?.RawJsonLd,
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

    private RecipeExtractionResult? ExtractRecipe(IDocument document)
    {
        var scripts = document
            .QuerySelectorAll("script[type='application/ld+json']")
            .Select(s => s.TextContent);
        return recipeExtractor.TryExtract(scripts);
    }
}
