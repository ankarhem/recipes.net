using Temporalio.Client;

namespace App.Notifications;

public sealed class NotificationService(ITemporalClient client, string taskQueue)
    : INotificationService
{
    public async Task SendAsync(
        SendNotificationCommand command,
        CancellationToken cancellationToken = default
    )
    {
        await client.StartWorkflowAsync(
            (NotificationWorkflow wf) => wf.RunAsync(command),
            new(id: $"notification-{Guid.NewGuid()}", taskQueue)
            {
                Rpc = new() { CancellationToken = cancellationToken },
            }
        );
    }
}
