namespace App.Crawler;

public interface ICrawlerService
{
    Task<WorkflowId> StartAsync(
        StartCrawlJobCommand command,
        CancellationToken cancellationToken = default
    );

    Task PauseAsync(WorkflowId workflowId, CancellationToken cancellationToken = default);

    Task ResumeAsync(WorkflowId workflowId, CancellationToken cancellationToken = default);
}
