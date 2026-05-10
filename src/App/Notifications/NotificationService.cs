using Microsoft.Extensions.Logging;
using Temporalio.Api.Enums.V1;
using Temporalio.Client;
using Temporalio.Exceptions;

namespace App.Notifications;

public sealed class NotificationService(
    ILogger<NotificationService> logger,
    ITemporalClient client,
    string taskQueue
) : INotificationService
{
    private readonly ILogger<NotificationService> _logger = logger;

    public async Task<string> SendAsync(
        SendNotificationCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var workflowId = command.NotificationId!;
        _logger.LogInformation(
            "Starting notification workflow {WorkflowId} for {TargetUrl}",
            workflowId,
            command.TargetUrl
        );

        try
        {
            var handle = await client.StartWorkflowAsync(
                (NotificationWorkflow wf) => wf.RunAsync(command),
                new(id: workflowId, taskQueue)
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
                "Notification workflow {WorkflowId} already exists for {TargetUrl}",
                workflowId,
                command.TargetUrl
            );

            return workflowId;
        }
    }
}
