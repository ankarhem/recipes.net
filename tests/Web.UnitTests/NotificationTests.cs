using System.Text.Json;
using App.Notifications;
using AwesomeAssertions;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Web.Controllers;
using Web.Models;
using Xunit;

namespace Web.Tests;

public class NotificationTests
{
    private static (NotificationsController controller, INotificationService mock) CreateSut()
    {
        var mock = Substitute.For<INotificationService>();
        return (new NotificationsController(mock), mock);
    }

    private static JsonElement JsonBody(string json) =>
        JsonSerializer.Deserialize<JsonElement>(json);

    [Fact]
    public async Task Post_Returns202()
    {
        var (controller, _) = CreateSut();

        var result = await controller.Post(
            new NotificationEnvelopeRequest
            {
                Body = JsonBody("""{"message": "hello"}"""),
                Metadata = new NotificationMetadata
                {
                    TargetUrl = new Uri("https://example.com/webhook"),
                },
            },
            CancellationToken.None
        );

        result.Should().BeOfType<AcceptedResult>();
    }

    [Fact]
    public async Task Post_ForwardsToNotificationService()
    {
        var (controller, mock) = CreateSut();

        await controller.Post(
            new NotificationEnvelopeRequest
            {
                Body = JsonBody("""{"message": "hello"}"""),
                Metadata = new NotificationMetadata
                {
                    TargetUrl = new Uri("https://example.com/webhook"),
                    Headers = new Dictionary<string, string> { ["X-Custom"] = "value" },
                },
            },
            CancellationToken.None
        );

        await mock.Received(1)
            .SendAsync(
                Arg.Is<SendNotificationCommand>(cmd =>
                    cmd.TargetUrl == new Uri("https://example.com/webhook")
                    && cmd.Headers!["X-Custom"] == "value"
                ),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task Post_WithDelay_ForwardsParsedDelay()
    {
        var (controller, mock) = CreateSut();

        await controller.Post(
            new NotificationEnvelopeRequest
            {
                Body = JsonBody("""{"message": "delayed"}"""),
                Metadata = new NotificationMetadata
                {
                    TargetUrl = new Uri("https://example.com/webhook"),
                    Delay =
                        TimeSpan.FromHours(1) + TimeSpan.FromMinutes(30) + TimeSpan.FromSeconds(15),
                },
            },
            CancellationToken.None
        );

        await mock.Received(1)
            .SendAsync(
                Arg.Is<SendNotificationCommand>(cmd =>
                    cmd.Delay
                    == TimeSpan.FromHours(1) + TimeSpan.FromMinutes(30) + TimeSpan.FromSeconds(15)
                ),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task Post_WithZeroDelay_ForwardsZeroDelay()
    {
        var (controller, mock) = CreateSut();

        await controller.Post(
            new NotificationEnvelopeRequest
            {
                Body = JsonBody("""{"message": "zero delay"}"""),
                Metadata = new NotificationMetadata
                {
                    TargetUrl = new Uri("https://example.com/webhook"),
                    Delay = TimeSpan.Zero,
                },
            },
            CancellationToken.None
        );

        await mock.Received(1)
            .SendAsync(
                Arg.Is<SendNotificationCommand>(cmd => cmd.Delay == TimeSpan.Zero),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task Post_WithoutDelay_DelayIsNull()
    {
        var (controller, mock) = CreateSut();

        await controller.Post(
            new NotificationEnvelopeRequest
            {
                Body = JsonBody("""{"message": "hello"}"""),
                Metadata = new NotificationMetadata
                {
                    TargetUrl = new Uri("https://example.com/webhook"),
                },
            },
            CancellationToken.None
        );

        await mock.Received(1)
            .SendAsync(
                Arg.Is<SendNotificationCommand>(cmd => cmd.Delay == null),
                Arg.Any<CancellationToken>()
            );
    }
}
