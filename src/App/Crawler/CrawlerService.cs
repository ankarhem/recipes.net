using Microsoft.Extensions.Logging;
using NanoidDotNet;
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
        var workflowId = $"{command.TargetUrl.Host}-{Nanoid.Generate(size: 10)}";

        try
        {
            var handle = await client.StartWorkflowAsync(
                (CrawlerWorkflow wf) => wf.RunAsync(command),
                new(id: workflowId, taskQueue)
                {
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
