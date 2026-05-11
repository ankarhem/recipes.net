using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Temporalio.Activities;
using Temporalio.Exceptions;

namespace App.Crawler;

public sealed class CrawlerActivities
{
    private readonly ILogger<CrawlerActivities> _logger;
    private readonly ICrawlerClient _client;

    public CrawlerActivities(ICrawlerClient client, ILogger<CrawlerActivities> logger)
    {
        _client = client;
        _logger = logger;
    }

    [Activity]
    public async Task<string> GetRecipePage(Uri url)
    {
        var cancellationToken = ActivityExecutionContext.Current.CancellationToken;
        _logger.LogInformation("Crawling {url}", url);

        try
        {
            var result = await _client.CrawlAsync(url, cancellationToken);

            _logger.LogInformation("Crawl of {url} completed successfully", url);
            return result;
        }
        catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode statusCode)
        {
            var nonRetryable = (int)statusCode is >= 400 and < 500 and not (408 or 429);
            _logger.LogWarning(
                ex,
                "Crawl of {url} failed with HTTP {StatusCode}",
                url,
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
            _logger.LogWarning(ex, "Crawl of {url} failed: network error", url);

            throw new ApplicationFailureException(
                $"Crawl failed: network error: {ex.Message}",
                inner: ex,
                errorType: null,
                nonRetryable: false
            );
        }
    }
}
