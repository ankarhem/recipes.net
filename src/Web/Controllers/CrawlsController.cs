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

        return Accepted(new { workflowId });
    }

    private static bool IsHttpScheme(Uri uri) =>
        uri.IsAbsoluteUri && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
