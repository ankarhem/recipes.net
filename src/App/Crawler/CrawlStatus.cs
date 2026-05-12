namespace App.Crawler;

public sealed record CrawlStatus(string Status, int UrlsCrawled, int UrlsQueued, bool IsPaused);
