using System.Linq;
using System.Text.Json;
using App.Notifications;
using AwesomeAssertions;
using Temporalio.Activities;
using Temporalio.Client;
using Temporalio.Exceptions;
using Temporalio.Testing;
using Temporalio.Worker;
using Xunit;

namespace App.WorkflowTests;

public class NotificationWorkflowTests
{
    private static SendNotificationCommand CreateCommand(TimeSpan? delay = null) =>
        new()
        {
            Body = JsonSerializer.Deserialize<JsonElement>("""{"message": "hello"}"""),
            TargetUrl = new Uri("https://example.com/webhook"),
            Headers = new Dictionary<string, string> { ["X-Custom"] = "value" },
            Delay = delay,
        };

    [Fact]
    public async Task Run_WithoutDelay_CallsActivityOnce()
    {
        await using var env = await WorkflowEnvironment.StartLocalAsync();

        var callCount = 0;
        SendNotificationCommand? captured = null;

        [Activity("SendNotification")]
        Task MockSendNotification(SendNotificationCommand command)
        {
            Interlocked.Increment(ref callCount);
            captured = command;
            return Task.CompletedTask;
        }

        using var worker = new TemporalWorker(
            env.Client,
            new TemporalWorkerOptions($"tq-{Guid.NewGuid()}")
                .AddWorkflow<NotificationWorkflow>()
                .AddActivity(MockSendNotification)
        );

        await worker.ExecuteAsync(async () =>
        {
            await env.Client.ExecuteWorkflowAsync(
                (NotificationWorkflow wf) => wf.RunAsync(CreateCommand()),
                new(id: $"wf-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!)
            );
        });

        callCount.Should().Be(1);
        captured.Should().NotBeNull();
        captured!.TargetUrl.Should().Be(new Uri("https://example.com/webhook"));
        captured.Headers.Should().ContainKey("X-Custom").WhoseValue.Should().Be("value");
        captured.Body.GetProperty("message").GetString().Should().Be("hello");
        captured.Delay.Should().BeNull();
    }

    [Fact]
    public async Task Run_WithDelay_StartsTimerBeforeCallingActivity()
    {
        await using var env = await WorkflowEnvironment.StartTimeSkippingAsync();

        var callCount = 0;

        [Activity("SendNotification")]
        Task MockSendNotification(SendNotificationCommand command)
        {
            Interlocked.Increment(ref callCount);
            return Task.CompletedTask;
        }

        using var worker = new TemporalWorker(
            env.Client,
            new TemporalWorkerOptions($"tq-{Guid.NewGuid()}")
                .AddWorkflow<NotificationWorkflow>()
                .AddActivity(MockSendNotification)
        );

        await worker.ExecuteAsync(async () =>
        {
            var handle = await env.Client.StartWorkflowAsync(
                (NotificationWorkflow wf) =>
                    wf.RunAsync(CreateCommand(delay: TimeSpan.FromHours(1))),
                new(id: $"wf-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!)
            );

            await handle.GetResultAsync();

            callCount.Should().Be(1);

            var history = await handle.FetchHistoryEventsAsync().ToListAsync();

            var timerStartedIndex = history.FindIndex(e => e.TimerStartedEventAttributes != null);
            var activityScheduledIndex = history.FindIndex(e =>
                e.ActivityTaskScheduledEventAttributes != null
            );

            timerStartedIndex
                .Should()
                .BeGreaterThanOrEqualTo(0, "workflow should have started a timer");
            activityScheduledIndex
                .Should()
                .BeGreaterThanOrEqualTo(0, "workflow should have scheduled an activity");
            timerStartedIndex
                .Should()
                .BeLessThan(
                    activityScheduledIndex,
                    "timer must start before activity is scheduled"
                );
        });
    }

    [Fact]
    public async Task Run_NonRetryableActivityFailure_FailsWorkflowImmediately()
    {
        await using var env = await WorkflowEnvironment.StartTimeSkippingAsync();

        var callCount = 0;

        [Activity("SendNotification")]
        Task MockFailingActivity(SendNotificationCommand command)
        {
            Interlocked.Increment(ref callCount);
            throw new ApplicationFailureException("webhook failed", nonRetryable: true);
        }

        using var worker = new TemporalWorker(
            env.Client,
            new TemporalWorkerOptions($"tq-{Guid.NewGuid()}")
                .AddWorkflow<NotificationWorkflow>()
                .AddActivity(MockFailingActivity)
        );

        await worker.ExecuteAsync(async () =>
        {
            var ex = await Assert.ThrowsAsync<WorkflowFailedException>(() =>
                env.Client.ExecuteWorkflowAsync(
                    (NotificationWorkflow wf) => wf.RunAsync(CreateCommand()),
                    new(id: $"wf-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!)
                )
            );

            callCount.Should().Be(1);

            var activityFailure = ex
                .InnerException.Should()
                .BeOfType<ActivityFailureException>()
                .Which;

            activityFailure
                .InnerException.Should()
                .BeOfType<ApplicationFailureException>()
                .Which.Message.Should()
                .Be("webhook failed");
        });
    }

    [Fact]
    public async Task Run_RetryableFailureThenSuccess_RetriesAndSucceeds()
    {
        await using var env = await WorkflowEnvironment.StartTimeSkippingAsync();

        var attempt = 0;

        [Activity("SendNotification")]
        Task MockFlakyActivity(SendNotificationCommand command)
        {
            Interlocked.Increment(ref attempt);
            if (attempt < 3)
                throw new ApplicationFailureException("transient error", nonRetryable: false);
            return Task.CompletedTask;
        }

        using var worker = new TemporalWorker(
            env.Client,
            new TemporalWorkerOptions($"tq-{Guid.NewGuid()}")
                .AddWorkflow<NotificationWorkflow>()
                .AddActivity(MockFlakyActivity)
        );

        await worker.ExecuteAsync(async () =>
        {
            await env.Client.ExecuteWorkflowAsync(
                (NotificationWorkflow wf) => wf.RunAsync(CreateCommand()),
                new(id: $"wf-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!)
            );
        });

        attempt.Should().Be(3);
    }

    [Fact]
    public async Task Run_RetryableFailureExhaustsAttempts_FailsWorkflow()
    {
        await using var env = await WorkflowEnvironment.StartTimeSkippingAsync();

        var attempt = 0;

        [Activity("SendNotification")]
        Task MockAlwaysFailingActivity(SendNotificationCommand command)
        {
            Interlocked.Increment(ref attempt);
            throw new ApplicationFailureException("persistent error", nonRetryable: false);
        }

        using var worker = new TemporalWorker(
            env.Client,
            new TemporalWorkerOptions($"tq-{Guid.NewGuid()}")
                .AddWorkflow<NotificationWorkflow>()
                .AddActivity(MockAlwaysFailingActivity)
        );

        await worker.ExecuteAsync(async () =>
        {
            await Assert.ThrowsAsync<WorkflowFailedException>(() =>
                env.Client.ExecuteWorkflowAsync(
                    (NotificationWorkflow wf) => wf.RunAsync(CreateCommand()),
                    new(id: $"wf-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!)
                )
            );
        });

        // Workflow configures MaximumAttempts = 5
        attempt.Should().Be(5);
    }
}
