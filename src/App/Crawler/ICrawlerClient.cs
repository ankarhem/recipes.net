namespace App.Crawler;

public interface ICrawlerClient
{
    Task<string?> GetPageAsync(Uri targetUrl, CancellationToken cancellationToken = default);
}
