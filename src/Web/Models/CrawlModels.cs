using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Web.Models;

public sealed record StartCrawlRequest
{
    [Required]
    [Description("The URL to start crawling from. Must use http or https scheme.")]
    public Uri? TargetUrl { get; init; }
}

public sealed record CrawlBadRequestResponse
{
    [Description("The error message.")]
    public required string Error { get; init; }
}

public sealed record CrawlStartedResponse
{
    [Description("The unique identifier of the crawl workflow.")]
    public required string WorkflowId { get; init; }
}

public sealed record CrawlPausedResponse
{
    [Description("The unique identifier of the crawl workflow.")]
    public required string WorkflowId { get; init; }

    [Description("The current status of the crawl.")]
    public required string Status { get; init; }
}

public sealed record CrawlResumedResponse
{
    [Description("The unique identifier of the crawl workflow.")]
    public required string WorkflowId { get; init; }

    [Description("The current status of the crawl.")]
    public required string Status { get; init; }
}

public sealed record CrawlStatusResponse
{
    [Description("The current status: running, paused, or completed.")]
    public required string Status { get; init; }

    [Description("Number of URLs that have been crawled.")]
    public required int UrlsCrawled { get; init; }

    [Description("Number of URLs queued for crawling.")]
    public required int UrlsQueued { get; init; }

    [Description("Whether the crawl is currently paused.")]
    public required bool IsPaused { get; init; }
}
