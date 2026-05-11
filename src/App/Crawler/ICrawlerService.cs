namespace App.Crawler;

public interface ICrawlerService
{
    Task<string> StartAsync(
        StartCrawlJobCommand command,
        CancellationToken cancellationToken = default
    );
}
