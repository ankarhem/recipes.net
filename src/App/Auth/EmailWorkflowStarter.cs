using Temporalio.Client;

namespace App.Auth;

public interface IEmailWorkflowStarter
{
    Task StartVerificationWorkflowAsync(
        Guid userId,
        string email,
        string token,
        CancellationToken cancellationToken = default
    );

    Task StartPasswordResetWorkflowAsync(
        Guid userId,
        string email,
        string token,
        CancellationToken cancellationToken = default
    );
}

public sealed class EmailWorkflowStarter(ITemporalClient client, string taskQueue)
    : IEmailWorkflowStarter
{
    public async Task StartVerificationWorkflowAsync(
        Guid userId,
        string email,
        string token,
        CancellationToken cancellationToken = default
    )
    {
        var workflowId = $"email-verification-{userId}-{TokenSuffix(token)}";
        await client.StartWorkflowAsync(
            (EmailVerificationWorkflow wf) => wf.RunAsync(userId, email, token),
            new(id: workflowId, taskQueue)
            {
                Rpc = new() { CancellationToken = cancellationToken },
            }
        );
    }

    public async Task StartPasswordResetWorkflowAsync(
        Guid userId,
        string email,
        string token,
        CancellationToken cancellationToken = default
    )
    {
        var workflowId = $"password-reset-{userId}-{TokenSuffix(token)}";
        await client.StartWorkflowAsync(
            (PasswordResetWorkflow wf) => wf.RunAsync(userId, email, token),
            new(id: workflowId, taskQueue)
            {
                Rpc = new() { CancellationToken = cancellationToken },
            }
        );
    }

    private static string TokenSuffix(string token) => TokenHasher.Hash(token)[..16];
}
