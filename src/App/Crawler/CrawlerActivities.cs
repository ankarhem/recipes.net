using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Temporalio.Activities;
using Temporalio.Exceptions;

namespace App.Crawler;

public sealed class CrawlerActivities(ICrawlerClient client, ILogger<CrawlerActivities> logger)
{
    private readonly ILogger<CrawlerActivities> _logger = logger;

    public CrawlerActivities(ICrawlerClient client)
        : this(client, NullLogger<CrawlerActivities>.Instance) { }

    [Activity]
    public async Task<string?> CrawlAsync(StartCrawlJobCommand command)
    {
        var cancellationToken = ActivityExecutionContext.Current.CancellationToken;
        _logger.LogInformation("Crawling {TargetUrl}", command.TargetUrl);

        try
        {
            var result = await client.GetPageAsync(command.TargetUrl, cancellationToken);

            _logger.LogInformation(
                "Crawl of {TargetUrl} completed successfully",
                command.TargetUrl
            );
            return result;
        }
        catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode statusCode)
        {
            var nonRetryable = (int)statusCode is >= 400 and < 500 and not (408 or 429);
            _logger.LogWarning(
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
            _logger.LogWarning(ex, "Crawl of {TargetUrl} failed: network error", command.TargetUrl);

            throw new ApplicationFailureException(
                $"Crawl failed: network error: {ex.Message}",
                inner: ex,
                errorType: null,
                nonRetryable: false
            );
        }
    }
}
