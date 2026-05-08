using System.Net;
using System.Text;
using System.Text.Json;
using App.Notifications;
using AwesomeAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using Xunit;

namespace Web.Tests;

public class NotificationTests
{
    private static (HttpClient client, INotificationService mock) CreateClient()
    {
        var mock = Substitute.For<INotificationService>();
        var factory = new TestWebApplicationFactory(mock);
        return (factory.CreateClient(), mock);
    }

    [Fact]
    public async Task Post_Notification_Returns202()
    {
        var (client, _) = CreateClient();

        var response = await client.PostAsync(
            "/api/v1/notification",
            new StringContent(CreateNotificationJson(), Encoding.UTF8, "application/json")
        );

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task Post_Notification_ForwardsToNotificationService()
    {
        var (client, mock) = CreateClient();

        await client.PostAsync(
            "/api/v1/notification",
            new StringContent(CreateNotificationJson(), Encoding.UTF8, "application/json")
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
    public async Task Post_Notification_WithDelay_ParsesIso8601Duration()
    {
        var (client, mock) = CreateClient();

        var json = """
            {
              "body": { "message": "delayed" },
              "metadata": {
                "targetUrl": "https://example.com/webhook",
                "delay": "PT1H30M15S"
              }
            }
            """;

        await client.PostAsync(
            "/api/v1/notification",
            new StringContent(json, Encoding.UTF8, "application/json")
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
    public async Task Post_Notification_WithZeroDelay_ParsesCorrectly()
    {
        var (client, mock) = CreateClient();

        var json = """
            {
              "body": { "message": "zero delay" },
              "metadata": {
                "targetUrl": "https://example.com/webhook",
                "delay": "PT0S"
              }
            }
            """;

        await client.PostAsync(
            "/api/v1/notification",
            new StringContent(json, Encoding.UTF8, "application/json")
        );

        await mock.Received(1)
            .SendAsync(
                Arg.Is<SendNotificationCommand>(cmd => cmd.Delay == TimeSpan.Zero),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task Post_Notification_WithoutDelay_DelayIsNull()
    {
        var (client, mock) = CreateClient();

        await client.PostAsync(
            "/api/v1/notification",
            new StringContent(CreateNotificationJson(), Encoding.UTF8, "application/json")
        );

        await mock.Received(1)
            .SendAsync(
                Arg.Is<SendNotificationCommand>(cmd => cmd.Delay == null),
                Arg.Any<CancellationToken>()
            );
    }

    private static string CreateNotificationJson() =>
        """
            {
              "body": { "message": "hello" },
              "metadata": {
                "targetUrl": "https://example.com/webhook",
                "headers": { "X-Custom": "value" }
              }
            }
            """;

    private class TestWebApplicationFactory(INotificationService mock)
        : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(
            Microsoft.AspNetCore.Hosting.IWebHostBuilder builder
        )
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll(typeof(INotificationService));
                services.AddSingleton(mock);
            });
        }
    }
}
