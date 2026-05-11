using System.Net;
using System.Text.Json;
using App.Notifications;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Temporalio.Client;
using Temporalio.Exceptions;
using Temporalio.Testing;
using Temporalio.Worker;
using Xunit;

namespace App.WorkflowTests;

public class NotificationActivityTests
{
    private static SendNotificationCommand CreateCommand() =>
        new()
        {
            Body = JsonSerializer.Deserialize<JsonElement>("""{"message": "hello"}"""),
            TargetUrl = new Uri("https://example.com/webhook"),
            Headers = new Dictionary<string, string> { ["X-Custom"] = "value" },
            Delay = null,
        };

    private static TemporalWorker CreateWorker(
        WorkflowEnvironment env,
        INotificationClient client
    ) =>
        new(
            env.Client,
            new TemporalWorkerOptions($"tq-{Guid.NewGuid()}")
                .AddWorkflow<NotificationWorkflow>()
                .AddAllActivities<NotificationActivities>(
                    new NotificationActivities(client, NullLogger<NotificationActivities>.Instance)
                )
        );

    [Fact]
    public async Task SendNotificationAsync_SuccessfulHttpResponse_Completes()
    {
        await using var env = await WorkflowEnvironment.StartTimeSkippingAsync();

        var client = Substitute.For<INotificationClient>();
        client.SendAsync(default!, default, default, default).ReturnsForAnyArgs(Task.CompletedTask);

        using var worker = CreateWorker(env, client);

        await worker.ExecuteAsync(async () =>
        {
            await env.Client.ExecuteWorkflowAsync(
                (NotificationWorkflow wf) => wf.RunAsync(CreateCommand()),
                new(id: $"wf-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!)
            );
        });

        await client
            .Received(1)
            .SendAsync(
                Arg.Is<Uri>(u => u == new Uri("https://example.com/webhook")),
                Arg.Is<JsonElement>(e => e.GetProperty("message").GetString() == "hello"),
                Arg.Is<Dictionary<string, string>?>(h => h!["X-Custom"] == "value"),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task SendNotificationAsync_5xxError_IsRetryable()
    {
        await using var env = await WorkflowEnvironment.StartTimeSkippingAsync();
        const string errorBody = "Internal Server Error";

        var client = Substitute.For<INotificationClient>();
        client
            .SendAsync(default!, default, default, default)
            .ReturnsForAnyArgs(_ =>
                throw new HttpRequestException(
                    $"server error: {errorBody}",
                    null,
                    HttpStatusCode.InternalServerError
                )
            );

        using var worker = CreateWorker(env, client);

        await worker.ExecuteAsync(async () =>
        {
            var ex = await Assert.ThrowsAsync<WorkflowFailedException>(() =>
                env.Client.ExecuteWorkflowAsync(
                    (NotificationWorkflow wf) => wf.RunAsync(CreateCommand()),
                    new(id: $"wf-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!)
                )
            );

            var appFailure = ex
                .InnerException.Should()
                .BeOfType<ActivityFailureException>()
                .Which.InnerException.Should()
                .BeOfType<ApplicationFailureException>()
                .Which;

            appFailure.Message.Should().Contain("HTTP 500");
            appFailure.Message.Should().Contain(errorBody);
            appFailure.NonRetryable.Should().BeFalse();
        });

        // Workflow retries MaximumAttempts = 5 times
        await client
            .Received(5)
            .SendAsync(
                Arg.Any<Uri>(),
                Arg.Any<JsonElement>(),
                Arg.Any<Dictionary<string, string>?>(),
                Arg.Any<CancellationToken>()
            );
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task SendNotificationAsync_ClientError_IsNonRetryable(HttpStatusCode statusCode)
    {
        await using var env = await WorkflowEnvironment.StartTimeSkippingAsync();
        const string errorBody = """{"error":"bad request"}""";

        var client = Substitute.For<INotificationClient>();
        client
            .SendAsync(default!, default, default, default)
            .ReturnsForAnyArgs(_ =>
                throw new HttpRequestException($"client error: {errorBody}", null, statusCode)
            );

        using var worker = CreateWorker(env, client);

        await worker.ExecuteAsync(async () =>
        {
            var ex = await Assert.ThrowsAsync<WorkflowFailedException>(() =>
                env.Client.ExecuteWorkflowAsync(
                    (NotificationWorkflow wf) => wf.RunAsync(CreateCommand()),
                    new(id: $"wf-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!)
                )
            );

            var appFailure = ex
                .InnerException.Should()
                .BeOfType<ActivityFailureException>()
                .Which.InnerException.Should()
                .BeOfType<ApplicationFailureException>()
                .Which;

            appFailure.Message.Should().Contain($"HTTP {(int)statusCode}");
            appFailure.Message.Should().Contain(errorBody);
            appFailure.NonRetryable.Should().BeTrue();
        });

        // Non-retryable: must only be called once
        await client
            .Received(1)
            .SendAsync(
                Arg.Any<Uri>(),
                Arg.Any<JsonElement>(),
                Arg.Any<Dictionary<string, string>?>(),
                Arg.Any<CancellationToken>()
            );
    }

    [Theory]
    [InlineData(HttpStatusCode.RequestTimeout)]
    [InlineData((HttpStatusCode)429)]
    public async Task SendNotificationAsync_RetryableClientError_IsRetryable(
        HttpStatusCode statusCode
    )
    {
        await using var env = await WorkflowEnvironment.StartTimeSkippingAsync();
        const string errorBody = """{"error":"retryable client error"}""";

        var client = Substitute.For<INotificationClient>();
        client
            .SendAsync(default!, default, default, default)
            .ReturnsForAnyArgs(_ =>
                throw new HttpRequestException(
                    $"retryable client error: {errorBody}",
                    null,
                    statusCode
                )
            );

        using var worker = CreateWorker(env, client);

        await worker.ExecuteAsync(async () =>
        {
            var ex = await Assert.ThrowsAsync<WorkflowFailedException>(() =>
                env.Client.ExecuteWorkflowAsync(
                    (NotificationWorkflow wf) => wf.RunAsync(CreateCommand()),
                    new(id: $"wf-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!)
                )
            );

            var appFailure = ex
                .InnerException.Should()
                .BeOfType<ActivityFailureException>()
                .Which.InnerException.Should()
                .BeOfType<ApplicationFailureException>()
                .Which;

            appFailure.Message.Should().Contain($"HTTP {(int)statusCode}");
            appFailure.Message.Should().Contain(errorBody);
            appFailure.NonRetryable.Should().BeFalse();
        });

        // Workflow retries MaximumAttempts = 5 times
        await client
            .Received(5)
            .SendAsync(
                Arg.Any<Uri>(),
                Arg.Any<JsonElement>(),
                Arg.Any<Dictionary<string, string>?>(),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task SendNotificationAsync_NetworkError_IsRetryable()
    {
        await using var env = await WorkflowEnvironment.StartTimeSkippingAsync();

        var client = Substitute.For<INotificationClient>();
        client
            .SendAsync(default!, default, default, default)
            .ReturnsForAnyArgs(_ => throw new HttpRequestException("connection refused"));

        using var worker = CreateWorker(env, client);

        await worker.ExecuteAsync(async () =>
        {
            var ex = await Assert.ThrowsAsync<WorkflowFailedException>(() =>
                env.Client.ExecuteWorkflowAsync(
                    (NotificationWorkflow wf) => wf.RunAsync(CreateCommand()),
                    new(id: $"wf-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!)
                )
            );

            var appFailure = ex
                .InnerException.Should()
                .BeOfType<ActivityFailureException>()
                .Which.InnerException.Should()
                .BeOfType<ApplicationFailureException>()
                .Which;

            appFailure.Message.Should().Contain("network error");
            appFailure.NonRetryable.Should().BeFalse();
        });

        // Workflow retries MaximumAttempts = 5 times
        await client
            .Received(5)
            .SendAsync(
                Arg.Any<Uri>(),
                Arg.Any<JsonElement>(),
                Arg.Any<Dictionary<string, string>?>(),
                Arg.Any<CancellationToken>()
            );
    }
}
