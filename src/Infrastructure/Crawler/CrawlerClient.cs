using App.Crawler;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Crawler;

public sealed class CrawlerClient(HttpClient httpClient, ILogger<CrawlerClient> logger)
    : ICrawlerClient
{
    private const int MaxErrorResponseBodyLength = 2048;
    private readonly ILogger<CrawlerClient> _logger = logger;

    public async Task<string?> GetPageAsync(
        Uri targetUrl,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogDebug("Sending HTTP GET to {TargetUrl}", targetUrl);

        using var response = await httpClient.GetAsync(targetUrl, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            if (responseBody.Length > MaxErrorResponseBodyLength)
            {
                responseBody = responseBody[..MaxErrorResponseBodyLength];
            }

            _logger.LogWarning(
                "HTTP GET to {TargetUrl} failed with {StatusCode}: {ResponseBody}",
                targetUrl,
                (int)response.StatusCode,
                responseBody
            );
            throw new HttpRequestException(
                $"HTTP {(int)response.StatusCode}: {responseBody}",
                null,
                response.StatusCode
            );
        }

        _logger.LogDebug(
            "HTTP GET to {TargetUrl} succeeded with {StatusCode}",
            targetUrl,
            response.StatusCode
        );

        return responseBody;
    }
}
