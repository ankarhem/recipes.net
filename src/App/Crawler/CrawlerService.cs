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

        var status = MapStatus(description.Status);

        if (status != CrawlRunStatus.Running)
        {
            return new CrawlStatus
            {
                Status = status,
                State = new CrawlState { UrlsCrawled = 0, UrlsQueued = 0 },
            };
        }

        var state = await handle.QueryAsync(wf => wf.GetState());
        var isPaused = await handle.QueryAsync(wf => wf.IsPaused);
        return new CrawlStatus
        {
            Status = isPaused ? CrawlRunStatus.Paused : CrawlRunStatus.Running,
            State = state,
        };
    }

#pragma warning disable CS8524 // Intentional: future Temporal enum values should surface as Unspecified
    private static CrawlRunStatus MapStatus(WorkflowExecutionStatus status) =>
        status switch
        {
            WorkflowExecutionStatus.Unspecified => CrawlRunStatus.Unspecified,
            WorkflowExecutionStatus.Running => CrawlRunStatus.Running,
            WorkflowExecutionStatus.Completed => CrawlRunStatus.Completed,
            WorkflowExecutionStatus.Failed => CrawlRunStatus.Failed,
            WorkflowExecutionStatus.Canceled => CrawlRunStatus.Canceled,
            WorkflowExecutionStatus.Terminated => CrawlRunStatus.Terminated,
            WorkflowExecutionStatus.ContinuedAsNew => CrawlRunStatus.ContinuedAsNew,
            WorkflowExecutionStatus.TimedOut => CrawlRunStatus.TimedOut,
            WorkflowExecutionStatus.Paused => CrawlRunStatus.Paused,
        };
#pragma warning restore CS8524
}
