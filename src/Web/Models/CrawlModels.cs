using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Web.Models;

public sealed record StartCrawlRequest
{
    [Required]
    [Description("The URL to start crawling from. Must use http or https scheme.")]
    public Uri? TargetUrl { get; init; }
}

public sealed record CrawlBadRequestResponse(
    [property: Description("The error message.")] string Error
);

public sealed record CrawlStartedResponse(
    [property: Description("The unique identifier of the crawl workflow.")] string WorkflowId
);

public sealed record CrawlPausedResponse(
    [property: Description("The unique identifier of the crawl workflow.")] string WorkflowId,
    [property: Description("The current status of the crawl.")] string Status
);

public sealed record CrawlResumedResponse(
    [property: Description("The unique identifier of the crawl workflow.")] string WorkflowId,
    [property: Description("The current status of the crawl.")] string Status
);

public sealed record CrawlStatusResponse(
    [property: Description("The current status: running, paused, or completed.")] string Status,
    [property: Description("Number of URLs that have been crawled.")] int UrlsCrawled,
    [property: Description("Number of URLs queued for crawling.")] int UrlsQueued,
    [property: Description("Whether the crawl is currently paused.")] bool IsPaused
);
