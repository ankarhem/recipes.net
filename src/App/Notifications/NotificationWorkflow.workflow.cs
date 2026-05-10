using Temporalio.Workflows;

namespace App.Notifications;

[Workflow]
public class NotificationWorkflow
{
    [WorkflowRun]
    public async Task RunAsync(SendNotificationCommand command)
    {
        if (command.Delay is { } delay)
        {
            await Workflow.DelayAsync(delay);
        }

        await Workflow.ExecuteActivityAsync(
            (NotificationActivities a) => a.SendNotificationAsync(command),
            new()
            {
                StartToCloseTimeout = TimeSpan.FromMinutes(5),
                RetryPolicy = new()
                {
                    InitialInterval = TimeSpan.FromSeconds(1),
                    BackoffCoefficient = 2.0F,
                    MaximumInterval = TimeSpan.FromSeconds(30),
                    MaximumAttempts = 5,
                },
            }
        );
    }
}
