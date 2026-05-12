using App.Crawler;
using AwesomeAssertions;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Web.Controllers;
using Web.Models;
using Xunit;

namespace Web.Tests;

public class CrawlsControllerTests
{
    private readonly ICrawlerService _service = Substitute.For<ICrawlerService>();
    private static readonly WorkflowId TestWorkflowId = new("example.com-VaK5fP3m9g");

    [Fact]
    public async Task Post_ValidRequest_Returns202WithWorkflowId()
    {
        _service
            .StartAsync(default!, default)
            .ReturnsForAnyArgs(
                (Func<NSubstitute.Core.CallInfo, Task<WorkflowId>>)(
                    _ => Task.FromResult(TestWorkflowId)
                )
            );
        var controller = new CrawlsController(_service);
        var request = new StartCrawlRequest { TargetUrl = new Uri("https://example.com") };

        var result = await controller.Post(request, CancellationToken.None);

        var accepted = result.Should().BeOfType<AcceptedResult>().Subject;
        accepted.StatusCode.Should().Be(202);
        accepted.Value.Should().BeEquivalentTo(new { workflowId = TestWorkflowId.Value });
    }

    [Fact]
    public async Task Post_ValidRequest_PassesTargetUrlToService()
    {
        _service
            .StartAsync(default!, default)
            .ReturnsForAnyArgs(
                (Func<NSubstitute.Core.CallInfo, Task<WorkflowId>>)(
                    _ => Task.FromResult(TestWorkflowId)
                )
            );
        var controller = new CrawlsController(_service);
        var targetUrl = new Uri("https://example.com/recipes");
        var request = new StartCrawlRequest { TargetUrl = targetUrl };

        await controller.Post(request, CancellationToken.None);

        await _service
            .Received(1)
            .StartAsync(
                Arg.Is<StartCrawlJobCommand>(c => c.TargetUrl == targetUrl),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task Post_ValidRequest_ForwardsCancellationToken()
    {
        _service
            .StartAsync(default!, default)
            .ReturnsForAnyArgs(
                (Func<NSubstitute.Core.CallInfo, Task<WorkflowId>>)(
                    _ => Task.FromResult(TestWorkflowId)
                )
            );
        var controller = new CrawlsController(_service);
        var request = new StartCrawlRequest { TargetUrl = new Uri("https://example.com") };
        using var cts = new CancellationTokenSource();

        await controller.Post(request, cts.Token);

        await _service.Received(1).StartAsync(Arg.Any<StartCrawlJobCommand>(), cts.Token);
    }

    [Theory]
    [InlineData("ftp://example.com")]
    [InlineData("file:///etc/passwd")]
    [InlineData("ws://example.com")]
    public async Task Post_NonHttpScheme_Returns400(string url)
    {
        var controller = new CrawlsController(_service);
        var request = new StartCrawlRequest { TargetUrl = new Uri(url) };

        var result = await controller.Post(request, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>().Which.StatusCode.Should().Be(400);
        await _service.DidNotReceiveWithAnyArgs().StartAsync(default!, default);
    }

    [Fact]
    public async Task Post_HttpsScheme_IsAccepted()
    {
        _service
            .StartAsync(default!, default)
            .ReturnsForAnyArgs(
                (Func<NSubstitute.Core.CallInfo, Task<WorkflowId>>)(
                    _ => Task.FromResult(TestWorkflowId)
                )
            );
        var controller = new CrawlsController(_service);
        var request = new StartCrawlRequest { TargetUrl = new Uri("https://example.com") };

        var result = await controller.Post(request, CancellationToken.None);

        result.Should().BeOfType<AcceptedResult>();
    }

    [Fact]
    public async Task Post_HttpScheme_IsAccepted()
    {
        _service
            .StartAsync(default!, default)
            .ReturnsForAnyArgs(
                (Func<NSubstitute.Core.CallInfo, Task<WorkflowId>>)(
                    _ => Task.FromResult(TestWorkflowId)
                )
            );
        var controller = new CrawlsController(_service);
        var request = new StartCrawlRequest { TargetUrl = new Uri("http://example.com") };

        var result = await controller.Post(request, CancellationToken.None);

        result.Should().BeOfType<AcceptedResult>();
    }
}
