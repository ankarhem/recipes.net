using Microsoft.Extensions.Logging;
using Temporalio.Api.Enums.V1;
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

    public async Task PauseAsync(
        WorkflowId workflowId,
        CancellationToken cancellationToken = default
    )
    {
        var handle = client.GetWorkflowHandle<CrawlerWorkflow>(workflowId.Value);
        await handle.SignalAsync(
            wf => wf.PauseAsync(),
            new() { Rpc = new() { CancellationToken = cancellationToken } }
        );
    }

    public async Task ResumeAsync(
        WorkflowId workflowId,
        CancellationToken cancellationToken = default
    )
    {
        var handle = client.GetWorkflowHandle<CrawlerWorkflow>(workflowId.Value);
        await handle.SignalAsync(
            wf => wf.ResumeAsync(),
            new() { Rpc = new() { CancellationToken = cancellationToken } }
        );
    }

    public async Task<CrawlStatus> GetStatusAsync(
        WorkflowId workflowId,
        CancellationToken cancellationToken = default
    )
    {
        var handle = client.GetWorkflowHandle<CrawlerWorkflow>(workflowId.Value);
        var description = await handle.DescribeAsync(
            new() { Rpc = new() { CancellationToken = cancellationToken } }
        );

        if (description.Status == WorkflowExecutionStatus.Completed)
        {
            return new CrawlStatus
            {
                Status = "completed",
                UrlsCrawled = 0,
                UrlsQueued = 0,
                IsPaused = false,
            };
        }

        return await handle.QueryAsync(wf => wf.GetStatus());
    }
}
