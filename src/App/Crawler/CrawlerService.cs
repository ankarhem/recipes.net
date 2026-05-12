using Microsoft.Extensions.Logging;
using Temporalio.Client;
using Temporalio.Exceptions;

namespace App.Crawler;

public sealed class CrawlerService(
    ILogger<CrawlerService> logger,
    ITemporalClient client,
    string taskQueue
) : ICrawlerService
{
    public async Task<WorkflowId> StartAsync(
        StartCrawlJobCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var workflowId = WorkflowId.Create(command.TargetUrl.Host);

        try
        {
            var handle = await client.StartWorkflowAsync(
                (CrawlerWorkflow wf) => wf.RunAsync(command),
                new(id: workflowId.Value, taskQueue)
                {
                    Rpc = new() { CancellationToken = cancellationToken },
                }
            );

            return workflowId;
        }
        catch (WorkflowAlreadyStartedException ex)
        {
            logger.LogInformation(ex, "Crawler for {TargetUrl} already started", command.TargetUrl);

            throw;
        }
    }
}
