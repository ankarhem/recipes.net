namespace App.Crawler;

public enum CrawlRunStatus
{
    Unspecified,
    Running,
    Completed,
    Failed,
    Canceled,
    Terminated,
    ContinuedAsNew,
    TimedOut,
    Paused,
}

public sealed record CrawlState
{
    public required int UrlsCrawled { get; init; }
    public required int UrlsQueued { get; init; }
}

public sealed record CrawlStatus
{
    public required CrawlRunStatus Status { get; init; }
    public required CrawlState State { get; init; }
}
