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
        var response = accepted.Value.Should().BeOfType<CrawlStartedResponse>().Subject;
        response.WorkflowId.Should().Be(TestWorkflowId.Value);
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

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.StatusCode.Should().Be(400);
        var response = badRequest.Value.Should().BeOfType<CrawlBadRequestResponse>().Subject;
        response.Error.Should().Be("TargetUrl must use http or https scheme.");
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

    [Fact]
    public async Task Pause_ValidId_Returns200WithPausedStatus()
    {
        var controller = new CrawlsController(_service);

        var result = await controller.Pause(TestWorkflowId.Value, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);
        var response = ok.Value.Should().BeOfType<CrawlPausedResponse>().Subject;
        response.WorkflowId.Should().Be(TestWorkflowId.Value);
        response.Status.Should().Be("paused");
    }

    [Fact]
    public async Task Pause_PassesWorkflowIdToService()
    {
        var controller = new CrawlsController(_service);

        await controller.Pause(TestWorkflowId.Value, CancellationToken.None);

        await _service
            .Received(1)
            .PauseAsync(
                Arg.Is<WorkflowId>(id => id.Value == TestWorkflowId.Value),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task Resume_ValidId_Returns200WithRunningStatus()
    {
        var controller = new CrawlsController(_service);

        var result = await controller.Resume(TestWorkflowId.Value, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);
        var response = ok.Value.Should().BeOfType<CrawlResumedResponse>().Subject;
        response.WorkflowId.Should().Be(TestWorkflowId.Value);
        response.Status.Should().Be("running");
    }

    [Fact]
    public async Task Resume_PassesWorkflowIdToService()
    {
        var controller = new CrawlsController(_service);

        await controller.Resume(TestWorkflowId.Value, CancellationToken.None);

        await _service
            .Received(1)
            .ResumeAsync(
                Arg.Is<WorkflowId>(id => id.Value == TestWorkflowId.Value),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task GetStatus_ReturnsStatusFromService()
    {
        var status = new CrawlStatus("running", 5, 3, false);
        _service
            .GetStatusAsync(Arg.Any<WorkflowId>(), Arg.Any<CancellationToken>())
            .Returns(
                (Func<NSubstitute.Core.CallInfo, Task<CrawlStatus>>)(_ => Task.FromResult(status))
            );
        var controller = new CrawlsController(_service);

        var result = await controller.GetStatus(TestWorkflowId.Value, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);
        var response = ok.Value.Should().BeOfType<CrawlStatusResponse>().Subject;
        response.Status.Should().Be("running");
        response.UrlsCrawled.Should().Be(5);
        response.UrlsQueued.Should().Be(3);
        response.IsPaused.Should().BeFalse();
    }

    [Fact]
    public async Task GetStatus_PassesWorkflowIdToService()
    {
        _service
            .GetStatusAsync(Arg.Any<WorkflowId>(), Arg.Any<CancellationToken>())
            .Returns(
                (Func<NSubstitute.Core.CallInfo, Task<CrawlStatus>>)(
                    _ => Task.FromResult(new CrawlStatus("running", 0, 0, false))
                )
            );
        var controller = new CrawlsController(_service);

        await controller.GetStatus(TestWorkflowId.Value, CancellationToken.None);

        await _service
            .Received(1)
            .GetStatusAsync(
                Arg.Is<WorkflowId>(id => id.Value == TestWorkflowId.Value),
                Arg.Any<CancellationToken>()
            );
    }
}
