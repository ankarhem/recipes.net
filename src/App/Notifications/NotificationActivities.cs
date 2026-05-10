using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Temporalio.Activities;
using Temporalio.Exceptions;

namespace App.Notifications;

public sealed class NotificationActivities(INotificationClient client)
{
    [Activity]
    public async Task SendNotificationAsync(SendNotificationCommand command)
    {
        var cancellationToken = ActivityExecutionContext.Current.CancellationToken;
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
            throw new ApplicationFailureException(
                $"Notification failed with HTTP {(int)statusCode}",
                errorType: null,
                nonRetryable: nonRetryable
            );
        }
        catch (HttpRequestException)
        {
            throw new ApplicationFailureException(
                "Notification failed: network error",
                errorType: null,
                nonRetryable: false
            );
        }
    }
}
