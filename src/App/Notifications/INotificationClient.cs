using System.Text.Json;

namespace App.Notifications;

public interface INotificationClient
{
    Task SendAsync(
        Uri targetUrl,
        JsonElement body,
        Dictionary<string, string>? headers,
        CancellationToken cancellationToken = default
    );
}
