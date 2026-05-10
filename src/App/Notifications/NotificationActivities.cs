using Temporalio.Activities;

namespace App.Notifications;

public sealed class NotificationActivities(INotificationClient client)
{
    [Activity]
    public Task SendNotificationAsync(SendNotificationCommand command)
    {
        return client.SendAsync(command.TargetUrl, command.Body, command.Headers, default);
    }
}
