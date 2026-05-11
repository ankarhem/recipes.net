using System.Net;
using Microsoft.Extensions.Logging;
using Schema.NET;
using Temporalio.Activities;
using Temporalio.Exceptions;

namespace App.Crawler;

public sealed class CrawlerActivities(
    ICrawlerClient client,
    IScraperService scraper,
    ILogger<CrawlerActivities> logger
)
{
    [Activity]
    public async Task<string?> CrawlAsync(StartCrawlJobCommand command)
    {
        var cancellationToken = ActivityExecutionContext.Current.CancellationToken;
        logger.LogInformation("Crawling {TargetUrl}", command.TargetUrl);

        try
        {
            var result = await client.GetPageAsync(command.TargetUrl, cancellationToken);

            logger.LogInformation("Crawl of {TargetUrl} completed successfully", command.TargetUrl);
            return result;
        }
        catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode statusCode)
        {
            var nonRetryable = (int)statusCode is >= 400 and < 500 and not (408 or 429);
            logger.LogWarning(
                ex,
                "Crawl of {TargetUrl} failed with HTTP {StatusCode}",
                command.TargetUrl,
                (int)statusCode
            );

            throw new ApplicationFailureException(
                $"Crawl failed with HTTP {(int)statusCode}: {ex.Message}",
                inner: ex,
                errorType: null,
                nonRetryable: nonRetryable
            );
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Crawl of {TargetUrl} failed: network error", command.TargetUrl);

            throw new ApplicationFailureException(
                $"Crawl failed: network error: {ex.Message}",
                inner: ex,
                errorType: null,
                nonRetryable: false
            );
        }
    }

    [Activity]
    public async Task<IReadOnlyList<Uri>> ExtractLinksAsync(string html, Uri baseUrl)
    {
        var ct = ActivityExecutionContext.Current.CancellationToken;
        return await scraper.ExtractLinksAsync(html, baseUrl, ct);
    }

    [Activity]
    public string? ExtractRecipeJsonLd(string html)
    {
        return scraper.ExtractRecipe(html) is { } recipe
            ? SchemaSerializer.SerializeObject(recipe)
            : null;
    }
}
