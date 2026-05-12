namespace App.Crawler;

public interface ICrawlerService
{
    Task<WorkflowId> StartAsync(
        StartCrawlJobCommand command,
        CancellationToken cancellationToken = default
    );
}
