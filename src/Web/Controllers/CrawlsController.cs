using App.Crawler;
using Microsoft.AspNetCore.Mvc;
using Web.Models;

namespace Web.Controllers;

[ApiController]
[Route("/crawls")]
public class CrawlsController(ICrawlerService crawlerService) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Post(
        [FromBody] StartCrawlRequest request,
        CancellationToken cancellationToken
    )
    {
        var targetUrl = request.TargetUrl!;

        if (!IsHttpScheme(targetUrl))
        {
            return BadRequest(
                new CrawlBadRequestResponse("TargetUrl must use http or https scheme.")
            );
        }

        var workflowId = await crawlerService.StartAsync(
            new StartCrawlJobCommand { TargetUrl = targetUrl },
            cancellationToken
        );

        return Accepted(new CrawlStartedResponse(workflowId.Value));
    }

    [HttpPost("{id}/pause")]
    public async Task<IActionResult> Pause(string id, CancellationToken cancellationToken)
    {
        var workflowId = new WorkflowId(id);
        await crawlerService.PauseAsync(workflowId, cancellationToken);

        return Ok(new CrawlPausedResponse(workflowId.Value, "paused"));
    }

    [HttpPost("{id}/resume")]
    public async Task<IActionResult> Resume(string id, CancellationToken cancellationToken)
    {
        var workflowId = new WorkflowId(id);
        await crawlerService.ResumeAsync(workflowId, cancellationToken);

        return Ok(new CrawlResumedResponse(workflowId.Value, "running"));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetStatus(string id, CancellationToken cancellationToken)
    {
        var workflowId = new WorkflowId(id);
        var status = await crawlerService.GetStatusAsync(workflowId, cancellationToken);

        return Ok(
            new CrawlStatusResponse(
                status.Status,
                status.UrlsCrawled,
                status.UrlsQueued,
                status.IsPaused
            )
        );
    }

    private static bool IsHttpScheme(Uri uri) =>
        uri.IsAbsoluteUri && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
