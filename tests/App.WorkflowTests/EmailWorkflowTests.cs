using App.Auth;
using AwesomeAssertions;
using NSubstitute;
using NSubstitute.Core;
using Temporalio.Client;
using Temporalio.Testing;
using Temporalio.Worker;
using Xunit;

namespace App.WorkflowTests;

public class EmailWorkflowTests
{
    private const string TestEmail = "user@example.com";
    private const string TestToken = "verification-token-abc";

    [Fact]
    public async Task EmailVerificationWorkflow_SendsVerificationEmail()
    {
        await using var env = await WorkflowEnvironment.StartTimeSkippingAsync();

        var emailService = Substitute.For<IEmailService>();
        emailService
            .SendEmailVerificationAsync(default!, default!, default)
            .ReturnsForAnyArgs((Func<CallInfo, Task>)(_ => Task.CompletedTask));

        var activities = new EmailActivities(emailService);

        using var worker = new TemporalWorker(
            env.Client,
            new TemporalWorkerOptions($"tq-{Guid.NewGuid()}")
                .AddWorkflow<EmailVerificationWorkflow>()
                .AddAllActivities(activities)
        );

        await worker.ExecuteAsync(async () =>
        {
            var userId = Guid.NewGuid();
            await env.Client.ExecuteWorkflowAsync(
                (EmailVerificationWorkflow wf) => wf.RunAsync(userId, TestEmail, TestToken),
                new(id: $"wf-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!)
            );
        });

        await emailService
            .Received(1)
            .SendEmailVerificationAsync(TestEmail, TestToken, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EmailVerificationWorkflow_RetriesFailedEmailSend()
    {
        await using var env = await WorkflowEnvironment.StartTimeSkippingAsync();

        var callCount = 0;
        var emailService = Substitute.For<IEmailService>();
        emailService
            .SendEmailVerificationAsync(default!, default!, default)
            .ReturnsForAnyArgs(
                (Func<CallInfo, Task>)(callInfo =>
                {
                    callCount++;
                    if (callCount == 1)
                    {
                        throw new InvalidOperationException("SMTP down");
                    }

                    return Task.CompletedTask;
                })
            );

        var activities = new EmailActivities(emailService);

        using var worker = new TemporalWorker(
            env.Client,
            new TemporalWorkerOptions($"tq-{Guid.NewGuid()}")
                .AddWorkflow<EmailVerificationWorkflow>()
                .AddAllActivities(activities)
        );

        await worker.ExecuteAsync(async () =>
        {
            var userId = Guid.NewGuid();
            await env.Client.ExecuteWorkflowAsync(
                (EmailVerificationWorkflow wf) => wf.RunAsync(userId, TestEmail, TestToken),
                new(id: $"wf-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!)
            );
        });

        callCount.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task PasswordResetWorkflow_SendsResetEmail()
    {
        await using var env = await WorkflowEnvironment.StartTimeSkippingAsync();

        var emailService = Substitute.For<IEmailService>();
        emailService
            .SendPasswordResetAsync(default!, default!, default)
            .ReturnsForAnyArgs((Func<CallInfo, Task>)(_ => Task.CompletedTask));

        var activities = new EmailActivities(emailService);

        using var worker = new TemporalWorker(
            env.Client,
            new TemporalWorkerOptions($"tq-{Guid.NewGuid()}")
                .AddWorkflow<PasswordResetWorkflow>()
                .AddAllActivities(activities)
        );

        await worker.ExecuteAsync(async () =>
        {
            var userId = Guid.NewGuid();
            await env.Client.ExecuteWorkflowAsync(
                (PasswordResetWorkflow wf) => wf.RunAsync(userId, TestEmail, TestToken),
                new(id: $"wf-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!)
            );
        });

        await emailService
            .Received(1)
            .SendPasswordResetAsync(TestEmail, TestToken, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PasswordResetWorkflow_RetriesFailedEmailSend()
    {
        await using var env = await WorkflowEnvironment.StartTimeSkippingAsync();

        var callCount = 0;
        var emailService = Substitute.For<IEmailService>();
        emailService
            .SendPasswordResetAsync(default!, default!, default)
            .ReturnsForAnyArgs(
                (Func<CallInfo, Task>)(callInfo =>
                {
                    callCount++;
                    if (callCount == 1)
                    {
                        throw new InvalidOperationException("SMTP down");
                    }

                    return Task.CompletedTask;
                })
            );

        var activities = new EmailActivities(emailService);

        using var worker = new TemporalWorker(
            env.Client,
            new TemporalWorkerOptions($"tq-{Guid.NewGuid()}")
                .AddWorkflow<PasswordResetWorkflow>()
                .AddAllActivities(activities)
        );

        await worker.ExecuteAsync(async () =>
        {
            var userId = Guid.NewGuid();
            await env.Client.ExecuteWorkflowAsync(
                (PasswordResetWorkflow wf) => wf.RunAsync(userId, TestEmail, TestToken),
                new(id: $"wf-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!)
            );
        });

        callCount.Should().BeGreaterThanOrEqualTo(2);
    }
}
