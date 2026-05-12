namespace App.Crawler;

public sealed record CrawlStatus
{
    public required string Status { get; init; }
    public required int UrlsCrawled { get; init; }
    public required int UrlsQueued { get; init; }
    public required bool IsPaused { get; init; }
}
