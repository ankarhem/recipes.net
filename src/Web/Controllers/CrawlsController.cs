using App.Crawler;
using Microsoft.AspNetCore.Mvc;
using Web.Models;

namespace Web.Controllers;

[ApiController]
[Route("/api/v1/crawls")]
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
                new CrawlBadRequestResponse { Error = "TargetUrl must use http or https scheme." }
            );
        }

        var workflowId = await crawlerService.StartAsync(
            new StartCrawlJobCommand { TargetUrl = targetUrl },
            cancellationToken
        );

        return Accepted(new CrawlStartedResponse { WorkflowId = workflowId.Value });
    }

    [HttpPost("{id}/pause")]
    public async Task<IActionResult> Pause(string id, CancellationToken cancellationToken)
    {
        var workflowId = new WorkflowId(id);
        await crawlerService.PauseAsync(workflowId, cancellationToken);

        return Ok(new CrawlPausedResponse { WorkflowId = workflowId.Value, Status = "paused" });
    }

    [HttpPost("{id}/resume")]
    public async Task<IActionResult> Resume(string id, CancellationToken cancellationToken)
    {
        var workflowId = new WorkflowId(id);
        await crawlerService.ResumeAsync(workflowId, cancellationToken);

        return Ok(new CrawlResumedResponse { WorkflowId = workflowId.Value, Status = "running" });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetStatus(string id, CancellationToken cancellationToken)
    {
        var workflowId = new WorkflowId(id);
        var status = await crawlerService.GetStatusAsync(workflowId, cancellationToken);

        return Ok(
            new CrawlStatusResponse
            {
                Status = status.Status,
                UrlsCrawled = status.UrlsCrawled,
                UrlsQueued = status.UrlsQueued,
                IsPaused = status.IsPaused,
            }
        );
    }

    private static bool IsHttpScheme(Uri uri) =>
        uri.IsAbsoluteUri && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
