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
            return BadRequest(new { error = "TargetUrl must use http or https scheme." });
        }

        var workflowId = await crawlerService.StartAsync(
            new StartCrawlJobCommand { TargetUrl = targetUrl },
            cancellationToken
        );

        return Accepted(new { workflowId = workflowId.Value });
    }

    [HttpPost("{id}/pause")]
    public async Task<IActionResult> Pause(string id, CancellationToken cancellationToken)
    {
        var workflowId = new WorkflowId(id);
        await crawlerService.PauseAsync(workflowId, cancellationToken);

        return Ok(new { workflowId = workflowId.Value, status = "paused" });
    }

    [HttpPost("{id}/resume")]
    public async Task<IActionResult> Resume(string id, CancellationToken cancellationToken)
    {
        var workflowId = new WorkflowId(id);
        await crawlerService.ResumeAsync(workflowId, cancellationToken);

        return Ok(new { workflowId = workflowId.Value, status = "running" });
    }

    private static bool IsHttpScheme(Uri uri) =>
        uri.IsAbsoluteUri && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
