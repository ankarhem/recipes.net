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
    public async Task<string> StartAsync(
        StartCrawlJobCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var host = command.TargetUrl.Host;

        try
        {
            var handle = await client.StartWorkflowAsync(
                (CrawlerWorkflow wf) => wf.RunAsync(command),
                new(id: host, taskQueue)
                {
                    IdReusePolicy = WorkflowIdReusePolicy.RejectDuplicate,
                    IdConflictPolicy = WorkflowIdConflictPolicy.UseExisting,
                    Rpc = new() { CancellationToken = cancellationToken },
                }
            );

            return handle.Id;
        }
        catch (WorkflowAlreadyStartedException ex)
        {
            logger.LogInformation(ex, "Crawler for {TargetUrl} already started", command.TargetUrl);

            throw;
        }
    }
}
