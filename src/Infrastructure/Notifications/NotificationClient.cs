using System.Net.Http.Json;
using System.Text.Json;
using App.Notifications;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Notifications;

public sealed class NotificationClient(HttpClient httpClient, ILogger<NotificationClient> logger)
    : INotificationClient
{
    private const int MaxErrorResponseBodyLength = 2048;

    private readonly ILogger<NotificationClient> _logger = logger;

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

        _logger.LogDebug("Sending HTTP POST to {TargetUrl}", targetUrl);
        using var response = await httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            if (responseBody.Length > MaxErrorResponseBodyLength)
            {
                responseBody = responseBody[..MaxErrorResponseBodyLength];
            }

            _logger.LogWarning(
                "HTTP POST to {TargetUrl} failed with {StatusCode}: {ResponseBody}",
                targetUrl,
                (int)response.StatusCode,
                responseBody
            );
            throw new HttpRequestException(
                $"HTTP {(int)response.StatusCode}: {responseBody}",
                null,
                response.StatusCode
            );
        }

        _logger.LogDebug(
            "HTTP POST to {TargetUrl} succeeded with {StatusCode}",
            targetUrl,
            response.StatusCode
        );
    }
}
