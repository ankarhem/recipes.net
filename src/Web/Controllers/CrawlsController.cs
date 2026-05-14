using App.Crawler;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Temporalio.Exceptions;
using Web.Models;

namespace Web.Controllers;

[ApiController]
[Authorize]
[Route("/api/v1/crawls")]
[Tags("Crawls")]
public class CrawlsController(ICrawlerService crawlerService) : ControllerBase
{
    /// <summary>
    /// Start a new crawl from a target URL.
    /// </summary>
    /// <response code="202">Crawl started successfully.</response>
    /// <response code="400">The request body is invalid or the URL scheme is not http/https.</response>
    /// <response code="401">Not authenticated.</response>
    /// <response code="409">A crawl for the same host is already running.</response>
    [HttpPost]
    [EnableRateLimiting("crawls")]
    [ProducesResponseType<CrawlStartedResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<CrawlBadRequestResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
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

        try
        {
            var workflowId = await crawlerService.StartAsync(
                new StartCrawlJobCommand { TargetUrl = targetUrl },
                cancellationToken
            );

            return Accepted(new CrawlStartedResponse { WorkflowId = workflowId.Value });
        }
        catch (WorkflowAlreadyStartedException)
        {
            return Conflict(
                new CrawlBadRequestResponse
                {
                    Error = $"A crawl for {targetUrl.Host} is already running.",
                }
            );
        }
    }

    /// <summary>
    /// Pause a running crawl.
    /// </summary>
    /// <response code="200">Crawl paused successfully.</response>
    /// <response code="401">Not authenticated.</response>
    [HttpPost("{id}/pause")]
    [ProducesResponseType<CrawlPausedResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Pause(string id, CancellationToken cancellationToken)
    {
        var workflowId = new WorkflowId(id);
        await crawlerService.PauseAsync(workflowId, cancellationToken);

        return Ok(
            new CrawlPausedResponse
            {
                WorkflowId = workflowId.Value,
                Status = CrawlRunStatus.Paused,
            }
        );
    }

    /// <summary>
    /// Resume a paused crawl.
    /// </summary>
    /// <response code="200">Crawl resumed successfully.</response>
    /// <response code="401">Not authenticated.</response>
    [HttpPost("{id}/resume")]
    [ProducesResponseType<CrawlResumedResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Resume(string id, CancellationToken cancellationToken)
    {
        var workflowId = new WorkflowId(id);
        await crawlerService.ResumeAsync(workflowId, cancellationToken);

        return Ok(
            new CrawlResumedResponse
            {
                WorkflowId = workflowId.Value,
                Status = CrawlRunStatus.Running,
            }
        );
    }

    /// <summary>
    /// Get the current status of a crawl.
    /// </summary>
    /// <response code="200">Returns the crawl status.</response>
    /// <response code="401">Not authenticated.</response>
    [HttpGet("{id}")]
    [ProducesResponseType<CrawlStatusResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetStatus(string id, CancellationToken cancellationToken)
    {
        var workflowId = new WorkflowId(id);
        var status = await crawlerService.GetStatusAsync(workflowId, cancellationToken);

        return Ok(
            new CrawlStatusResponse
            {
                Status = status.Status,
                UrlsCrawled = status.State.UrlsCrawled,
                UrlsQueued = status.State.UrlsQueued,
            }
        );
    }

    private static bool IsHttpScheme(Uri uri) =>
        uri.IsAbsoluteUri && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
