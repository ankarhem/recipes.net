using Temporalio.Workflows;

namespace App.Auth;

[Workflow]
public class PasswordResetWorkflow
{
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
    }
}
