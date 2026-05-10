namespace App.Notifications;

public interface INotificationService
{
    Task<string> SendAsync(
        SendNotificationCommand command,
        CancellationToken cancellationToken = default
    );
}
