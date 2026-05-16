using Temporalio.Workflows;

namespace App.Identity.Workflows;

[Workflow]
public class PasswordResetWorkflow
{
    private static readonly TimeSpan CleanupDelay = TimeSpan.FromDays(30);

    [WorkflowRun]
    public async Task RunAsync(Guid userId, string email, string token)
    {
        await Workflow.ExecuteActivityAsync(
            (EmailActivities a) => a.SendPasswordResetEmailAsync(email, token),
            new()
            {
                StartToCloseTimeout = TimeSpan.FromSeconds(30),
                RetryPolicy = new()
                {
                    InitialInterval = TimeSpan.FromSeconds(5),
                    BackoffCoefficient = 2.0f,
                    MaximumInterval = TimeSpan.FromSeconds(60),
                    MaximumAttempts = 5,
                },
            }
        );

        await Workflow.DelayAsync(CleanupDelay);

        await Workflow.ExecuteActivityAsync(
            (TokenCleanupActivities a) => a.DeletePasswordResetTokenAsync(token),
            new()
            {
                StartToCloseTimeout = TimeSpan.FromSeconds(10),
                RetryPolicy = new()
                {
                    InitialInterval = TimeSpan.FromSeconds(5),
                    BackoffCoefficient = 2.0f,
                    MaximumInterval = TimeSpan.FromMinutes(5),
                    MaximumAttempts = 10,
                },
            }
        );
    }
}
