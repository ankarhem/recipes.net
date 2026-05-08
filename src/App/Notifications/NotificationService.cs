namespace App.Notifications;

public sealed class NotificationService(INotificationClient client) : INotificationService
{
    public async Task SendAsync(
        SendNotificationCommand command,
        CancellationToken cancellationToken = default
    )
    {
        await client.SendAsync(command.TargetUrl, command.Body, command.Headers, cancellationToken);
    }
}
