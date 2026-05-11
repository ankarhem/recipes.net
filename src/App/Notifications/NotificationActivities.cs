using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Temporalio.Activities;
using Temporalio.Exceptions;

namespace App.Notifications;

public sealed class NotificationActivities(
    INotificationClient client,
    ILogger<NotificationActivities> logger
)
{
    [Activity]
    public async Task SendNotificationAsync(SendNotificationCommand command)
    {
        var cancellationToken = ActivityExecutionContext.Current.CancellationToken;
        logger.LogInformation("Sending notification to {TargetUrl}", command.TargetUrl);

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
            logger.LogWarning(
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
            logger.LogWarning(
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

        logger.LogInformation("Notification sent successfully to {TargetUrl}", command.TargetUrl);
    }
}
