namespace App.Notifications;

public interface INotificationService
{
    Task SendAsync(SendNotificationCommand command, CancellationToken cancellationToken = default);
}
