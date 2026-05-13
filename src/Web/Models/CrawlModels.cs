using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using App.Crawler;

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
    public required CrawlRunStatus Status { get; init; }
}

public sealed record CrawlResumedResponse
{
    [Description("The unique identifier of the crawl workflow.")]
    public required string WorkflowId { get; init; }

    [Description("The current status of the crawl.")]
    public required CrawlRunStatus Status { get; init; }
}

public sealed record CrawlStatusResponse
{
    [Description("The current status of the crawl.")]
    public required CrawlRunStatus Status { get; init; }

    [Description(
        "Number of URLs that have been crawled. Only meaningful when status is Running or Paused."
    )]
    public required int UrlsCrawled { get; init; }

    [Description(
        "Number of URLs queued for crawling. Only meaningful when status is Running or Paused."
    )]
    public required int UrlsQueued { get; init; }
}
