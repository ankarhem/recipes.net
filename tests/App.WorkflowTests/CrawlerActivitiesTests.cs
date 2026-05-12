using System.Net;
using App.Crawler;
using App.Recipe;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Temporalio.Exceptions;
using Temporalio.Testing;
using Xunit;

namespace App.WorkflowTests;

public class CrawlerActivitiesTests
{
    private static CrawlerActivities CreateActivities(
        ICrawlerClient? client = null,
        IScraperService? scraper = null,
        IRecipeRepository? repository = null
    ) =>
        new(
            client ?? Substitute.For<ICrawlerClient>(),
            scraper ?? Substitute.For<IScraperService>(),
            repository ?? Substitute.For<IRecipeRepository>(),
            NullLogger<CrawlerActivities>.Instance
        );

    [Fact]
    public async Task FetchPageAsync_Success_ReturnsContent()
    {
        var client = Substitute.For<ICrawlerClient>();
        client.GetPageAsync(default!, default).ReturnsForAnyArgs("<html>hello</html>");

        var activities = CreateActivities(client: client);
        var env = new ActivityEnvironment();

        var result = await env.RunAsync(() =>
            activities.FetchPageAsync(new Uri("https://example.com"))
        );

        result.Should().Be("<html>hello</html>");
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task FetchPageAsync_ClientError_IsNonRetryable(HttpStatusCode statusCode)
    {
        var client = Substitute.For<ICrawlerClient>();
        client
            .GetPageAsync(default!, default)
            .ReturnsForAnyArgs(
                (Func<NSubstitute.Core.CallInfo, string?>)(
                    _ => throw new HttpRequestException("client error", null, statusCode)
                )
            );

        var activities = CreateActivities(client: client);
        var env = new ActivityEnvironment();

        var ex = await Assert.ThrowsAsync<ApplicationFailureException>(() =>
            env.RunAsync(() => activities.FetchPageAsync(new Uri("https://example.com")))
        );

        ex.Message.Should().Contain($"HTTP {(int)statusCode}");
        ex.NonRetryable.Should().BeTrue();
    }

    [Theory]
    [InlineData(HttpStatusCode.RequestTimeout)]
    [InlineData((HttpStatusCode)429)]
    public async Task FetchPageAsync_RetryableClientError_IsRetryable(HttpStatusCode statusCode)
    {
        var client = Substitute.For<ICrawlerClient>();
        client
            .GetPageAsync(default!, default)
            .ReturnsForAnyArgs(
                (Func<NSubstitute.Core.CallInfo, string?>)(
                    _ => throw new HttpRequestException("retryable", null, statusCode)
                )
            );

        var activities = CreateActivities(client: client);
        var env = new ActivityEnvironment();

        var ex = await Assert.ThrowsAsync<ApplicationFailureException>(() =>
            env.RunAsync(() => activities.FetchPageAsync(new Uri("https://example.com")))
        );

        ex.Message.Should().Contain($"HTTP {(int)statusCode}");
        ex.NonRetryable.Should().BeFalse();
    }

    [Fact]
    public async Task FetchPageAsync_ServerError_IsRetryable()
    {
        var client = Substitute.For<ICrawlerClient>();
        client
            .GetPageAsync(default!, default)
            .ReturnsForAnyArgs(
                (Func<NSubstitute.Core.CallInfo, string?>)(
                    _ =>
                        throw new HttpRequestException(
                            "server error",
                            null,
                            HttpStatusCode.BadGateway
                        )
                )
            );

        var activities = CreateActivities(client: client);
        var env = new ActivityEnvironment();

        var ex = await Assert.ThrowsAsync<ApplicationFailureException>(() =>
            env.RunAsync(() => activities.FetchPageAsync(new Uri("https://example.com")))
        );

        ex.Message.Should().Contain("HTTP 502");
        ex.NonRetryable.Should().BeFalse();
    }

    [Fact]
    public async Task FetchPageAsync_NetworkError_IsRetryable()
    {
        var client = Substitute.For<ICrawlerClient>();
        client
            .GetPageAsync(default!, default)
            .ReturnsForAnyArgs(
                (Func<NSubstitute.Core.CallInfo, string?>)(
                    _ => throw new HttpRequestException("connection refused")
                )
            );

        var activities = CreateActivities(client: client);
        var env = new ActivityEnvironment();

        var ex = await Assert.ThrowsAsync<ApplicationFailureException>(() =>
            env.RunAsync(() => activities.FetchPageAsync(new Uri("https://example.com")))
        );

        ex.Message.Should().Contain("network error");
        ex.NonRetryable.Should().BeFalse();
    }
}
