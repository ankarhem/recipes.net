using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Temporalio.Activities;
using Temporalio.Exceptions;

namespace App.Notifications;

public sealed class NotificationActivities(
    INotificationClient client,
    ILogger<NotificationActivities> logger
)
{
    private readonly ILogger<NotificationActivities> _logger = logger;

    public NotificationActivities(INotificationClient client)
        : this(client, NullLogger<NotificationActivities>.Instance) { }

    [Activity]
    public async Task SendNotificationAsync(SendNotificationCommand command)
    {
        var cancellationToken = ActivityExecutionContext.Current.CancellationToken;
        _logger.LogInformation("Sending notification to {TargetUrl}", command.TargetUrl);

        try
        {
            await client.SendAsync(
                command.TargetUrl,
                command.Body,
                command.Headers,
                cancellationToken
            );
        }
        catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode statusCode)
        {
            var nonRetryable = (int)statusCode is >= 400 and < 500 and not (408 or 429);
            _logger.LogWarning(
                ex,
                "Notification to {TargetUrl} failed with HTTP {StatusCode}",
                command.TargetUrl,
                (int)statusCode
            );

            throw new ApplicationFailureException(
                $"Notification failed with HTTP {(int)statusCode}: {ex.Message}",
                inner: ex,
                errorType: null,
                nonRetryable: nonRetryable
            );
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(
                ex,
                "Notification to {TargetUrl} failed: network error",
                command.TargetUrl
            );

            throw new ApplicationFailureException(
                $"Notification failed: network error: {ex.Message}",
                inner: ex,
                errorType: null,
                nonRetryable: false
            );
        }

        _logger.LogInformation("Notification sent successfully to {TargetUrl}", command.TargetUrl);
    }
}
