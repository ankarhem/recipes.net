using App.Identity;
using App.Identity.Ports;
using App.Identity.Workflows;
using AwesomeAssertions;
using Domain;
using Domain.Identity;
using NSubstitute;
using NSubstitute.Core;
using Temporalio.Client;
using Temporalio.Testing;
using Temporalio.Worker;
using Xunit;
using DomainUser = Domain.Identity.User;

namespace App.WorkflowTests;

public class EmailWorkflowTests
{
    private const string TestEmail = "user@example.com";
    private const string TestToken = "verification-token-abc";

    [Fact]
    public async Task EmailVerificationWorkflow_SendsVerificationEmail()
    {
        await using var env = await WorkflowEnvironment.StartTimeSkippingAsync();
        var emailService = CreateEmailService();
        var cleanupActivities = CreateNoopCleanupActivities();

        using var worker = new TemporalWorker(
            env.Client,
            new TemporalWorkerOptions($"tq-{Guid.NewGuid()}")
                .AddWorkflow<EmailVerificationWorkflow>()
                .AddAllActivities(new EmailActivities(emailService))
                .AddAllActivities(cleanupActivities)
        );

        await worker.ExecuteAsync(async () =>
        {
            await env.Client.ExecuteWorkflowAsync(
                (EmailVerificationWorkflow wf) => wf.RunAsync(Guid.NewGuid(), TestEmail, TestToken),
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
                (Func<CallInfo, Task>)(_ =>
                {
                    callCount++;
                    if (callCount == 1)
                    {
                        throw new InvalidOperationException("SMTP down");
                    }
                    return Task.CompletedTask;
                })
            );
        var cleanupActivities = CreateNoopCleanupActivities();

        using var worker = new TemporalWorker(
            env.Client,
            new TemporalWorkerOptions($"tq-{Guid.NewGuid()}")
                .AddWorkflow<EmailVerificationWorkflow>()
                .AddAllActivities(new EmailActivities(emailService))
                .AddAllActivities(cleanupActivities)
        );

        await worker.ExecuteAsync(async () =>
        {
            await env.Client.ExecuteWorkflowAsync(
                (EmailVerificationWorkflow wf) => wf.RunAsync(Guid.NewGuid(), TestEmail, TestToken),
                new(id: $"wf-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!)
            );
        });

        callCount.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task EmailVerificationWorkflow_DeletesTokenAfterDelay()
    {
        await using var env = await WorkflowEnvironment.StartTimeSkippingAsync();
        var emailService = CreateEmailService();
        var users = Substitute.For<IUserRepository>();
        var user = DomainUser.Register(
            Email.Normalize("user@example.com"),
            PasswordHash.From("hashed"),
            new TestClock()
        );
        user.IssueEmailVerificationToken(
            TokenHash.From(Domain.Identity.TokenHasher.Hash(TestToken)),
            DateTimeOffset.UtcNow.AddHours(24),
            new TestClock()
        );
        users
            .GetByEmailVerificationTokenHashAsync(default!, default)
            .ReturnsForAnyArgs((Func<CallInfo, Task<DomainUser?>>)(_ => Task.FromResult<DomainUser?>(user)));
        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork
            .SaveChangesAsync(default)
            .ReturnsForAnyArgs((Func<CallInfo, Task>)(_ => Task.CompletedTask));
        var cleanupActivities = new TokenCleanupActivities(users, unitOfWork, new TestClock());

        using var worker = new TemporalWorker(
            env.Client,
            new TemporalWorkerOptions($"tq-{Guid.NewGuid()}")
                .AddWorkflow<EmailVerificationWorkflow>()
                .AddAllActivities(new EmailActivities(emailService))
                .AddAllActivities(cleanupActivities)
        );

        await worker.ExecuteAsync(async () =>
        {
            await env.Client.ExecuteWorkflowAsync(
                (EmailVerificationWorkflow wf) => wf.RunAsync(Guid.NewGuid(), TestEmail, TestToken),
                new(id: $"wf-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!)
            );
        });

        user.EmailVerificationTokens.Should().BeEmpty();
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PasswordResetWorkflow_SendsResetEmail()
    {
        await using var env = await WorkflowEnvironment.StartTimeSkippingAsync();
        var emailService = CreateEmailService();
        var cleanupActivities = CreateNoopCleanupActivities();

        using var worker = new TemporalWorker(
            env.Client,
            new TemporalWorkerOptions($"tq-{Guid.NewGuid()}")
                .AddWorkflow<PasswordResetWorkflow>()
                .AddAllActivities(new EmailActivities(emailService))
                .AddAllActivities(cleanupActivities)
        );

        await worker.ExecuteAsync(async () =>
        {
            await env.Client.ExecuteWorkflowAsync(
                (PasswordResetWorkflow wf) => wf.RunAsync(Guid.NewGuid(), TestEmail, TestToken),
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
                (Func<CallInfo, Task>)(_ =>
                {
                    callCount++;
                    if (callCount == 1)
                    {
                        throw new InvalidOperationException("SMTP down");
                    }
                    return Task.CompletedTask;
                })
            );
        var cleanupActivities = CreateNoopCleanupActivities();

        using var worker = new TemporalWorker(
            env.Client,
            new TemporalWorkerOptions($"tq-{Guid.NewGuid()}")
                .AddWorkflow<PasswordResetWorkflow>()
                .AddAllActivities(new EmailActivities(emailService))
                .AddAllActivities(cleanupActivities)
        );

        await worker.ExecuteAsync(async () =>
        {
            await env.Client.ExecuteWorkflowAsync(
                (PasswordResetWorkflow wf) => wf.RunAsync(Guid.NewGuid(), TestEmail, TestToken),
                new(id: $"wf-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!)
            );
        });

        callCount.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task PasswordResetWorkflow_DeletesTokenAfterDelay()
    {
        await using var env = await WorkflowEnvironment.StartTimeSkippingAsync();
        var emailService = CreateEmailService();
        var users = Substitute.For<IUserRepository>();
        var user = DomainUser.Register(
            Email.Normalize("user@example.com"),
            PasswordHash.From("hashed"),
            new TestClock()
        );
        user.IssuePasswordResetToken(
            TokenHash.From(Domain.Identity.TokenHasher.Hash(TestToken)),
            DateTimeOffset.UtcNow.AddHours(1),
            new TestClock()
        );
        users
            .GetByPasswordResetTokenHashAsync(default!, default)
            .ReturnsForAnyArgs((Func<CallInfo, Task<DomainUser?>>)(_ => Task.FromResult<DomainUser?>(user)));
        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork
            .SaveChangesAsync(default)
            .ReturnsForAnyArgs((Func<CallInfo, Task>)(_ => Task.CompletedTask));
        var cleanupActivities = new TokenCleanupActivities(users, unitOfWork, new TestClock());

        using var worker = new TemporalWorker(
            env.Client,
            new TemporalWorkerOptions($"tq-{Guid.NewGuid()}")
                .AddWorkflow<PasswordResetWorkflow>()
                .AddAllActivities(new EmailActivities(emailService))
                .AddAllActivities(cleanupActivities)
        );

        await worker.ExecuteAsync(async () =>
        {
            await env.Client.ExecuteWorkflowAsync(
                (PasswordResetWorkflow wf) => wf.RunAsync(Guid.NewGuid(), TestEmail, TestToken),
                new(id: $"wf-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!)
            );
        });

        user.PasswordResetTokens.Should().BeEmpty();
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private static IEmailService CreateEmailService()
    {
        var emailService = Substitute.For<IEmailService>();
        emailService
            .SendEmailVerificationAsync(default!, default!, default)
            .ReturnsForAnyArgs((Func<CallInfo, Task>)(_ => Task.CompletedTask));
        emailService
            .SendPasswordResetAsync(default!, default!, default)
            .ReturnsForAnyArgs((Func<CallInfo, Task>)(_ => Task.CompletedTask));
        return emailService;
    }

    private static TokenCleanupActivities CreateNoopCleanupActivities()
    {
        var users = Substitute.For<IUserRepository>();
        users
            .GetByEmailVerificationTokenHashAsync(default!, default)
            .ReturnsForAnyArgs(
                (Func<CallInfo, Task<DomainUser?>>)(_ => Task.FromResult<DomainUser?>(null))
            );
        users
            .GetByPasswordResetTokenHashAsync(default!, default)
            .ReturnsForAnyArgs(
                (Func<CallInfo, Task<DomainUser?>>)(_ => Task.FromResult<DomainUser?>(null))
            );
        return new TokenCleanupActivities(users, Substitute.For<IUnitOfWork>(), new TestClock());
    }

    private sealed class TestClock : IClock
    {
        public DateTimeOffset UtcNow { get; } = DateTimeOffset.UtcNow;
    }
}
