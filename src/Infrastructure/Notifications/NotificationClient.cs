using System.Net.Http.Json;
using System.Text.Json;
using App.Notifications;

namespace Infrastructure.Notifications;

public sealed class NotificationClient(HttpClient httpClient) : INotificationClient
{
    public async Task SendAsync(
        Uri targetUrl,
        JsonElement body,
        Dictionary<string, string>? headers,
        CancellationToken cancellationToken = default
    )
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, targetUrl)
        {
            Content = JsonContent.Create(body),
        };

        if (headers is not null)
        {
            foreach (var (key, value) in headers)
            {
                request.Headers.TryAddWithoutValidation(key, value);
            }
        }

        await httpClient.SendAsync(request, cancellationToken);
    }
}
