using System.Net;
using App.Recipe;
using Microsoft.Extensions.Logging;
using Temporalio.Activities;
using Temporalio.Exceptions;
using DomainRecipe = Domain.Recipe.Recipe;

namespace App.Crawler;

public sealed class CrawlerActivities(
    ICrawlerClient client,
    IScraperService scraper,
    IRecipeRepository repository,
    ILogger<CrawlerActivities> logger
)
{
    [Activity]
    public async Task<string?> FetchPageAsync(Uri url)
    {
        var cancellationToken = ActivityExecutionContext.Current.CancellationToken;
        logger.LogInformation("Fetching {Url}", url);

        try
        {
            var result = await client.GetPageAsync(url, cancellationToken);

            logger.LogInformation("Fetched {Url} successfully", url);
            return result;
        }
        catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode statusCode)
        {
            var nonRetryable = (int)statusCode is >= 400 and < 500 and not (408 or 429);
            logger.LogWarning(
                ex,
                "Fetch of {Url} failed with HTTP {StatusCode}",
                url,
                (int)statusCode
            );

            throw new ApplicationFailureException(
                $"Fetch failed with HTTP {(int)statusCode}: {ex.Message}",
                inner: ex,
                errorType: null,
                nonRetryable: nonRetryable
            );
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Fetch of {Url} failed: network error", url);

            throw new ApplicationFailureException(
                $"Fetch failed: network error: {ex.Message}",
                inner: ex,
                errorType: null,
                nonRetryable: false
            );
        }
    }

    [Activity]
    public async Task<ExtractedPage> ExtractPageAsync(string html, Uri baseUrl)
    {
        var ct = ActivityExecutionContext.Current.CancellationToken;
        return await scraper.ExtractPageAsync(html, baseUrl, ct);
    }

    [Activity]
    public async Task<Guid> SaveRecipeAsync(
        DomainRecipe recipe,
        string sourceUrl,
        string rawSchemaJson
    )
    {
        var ct = ActivityExecutionContext.Current.CancellationToken;
        var id = await repository.SaveImportedAsync(recipe, sourceUrl, rawSchemaJson, ct);
        logger.LogInformation(
            "Saved recipe {Name} from {Url} (Id: {Id})",
            recipe.Name,
            sourceUrl,
            id
        );
        return id;
    }
}
