using App.Identity.Ports;
using Temporalio.Activities;

namespace App.Identity.Workflows;

public sealed class EmailActivities(IEmailService emailService)
{
    [Activity]
    public async Task SendVerificationEmailAsync(string email, string token)
    {
        var cancellationToken = ActivityExecutionContext.Current.CancellationToken;
        await emailService.SendEmailVerificationAsync(email, token, cancellationToken);
    }

    [Activity]
    public async Task SendPasswordResetEmailAsync(string email, string token)
    {
        var cancellationToken = ActivityExecutionContext.Current.CancellationToken;
        await emailService.SendPasswordResetAsync(email, token, cancellationToken);
    }
}
