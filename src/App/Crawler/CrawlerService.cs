using Microsoft.Extensions.Logging;
using Temporalio.Api.Enums.V1;
using Temporalio.Client;
using Temporalio.Exceptions;

namespace App.Crawler;

public sealed class CrawlerService
{
    private readonly ILogger<CrawlerService> _logger;
    private readonly ITemporalClient _client;
    private readonly string _taskQueue;

    public CrawlerService(ILogger<CrawlerService> logger, ITemporalClient client, string taskQueue)
    {
        _logger = logger;
        _client = client;
        _taskQueue = taskQueue;
    }

    public async Task<string> StartAsync(
        StartCrawlJobCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var host = command.TargetUrl.Host;

        try
        {
            var handle = await _client.StartWorkflowAsync(
                (CrawlerWorkflow wf) => wf.RunAsync(command),
                new(id: host, _taskQueue)
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
            _logger.LogInformation(
                ex,
                "Crawler for {TargetUrl} already started",
                command.TargetUrl
            );

            throw;
        }
    }
}
