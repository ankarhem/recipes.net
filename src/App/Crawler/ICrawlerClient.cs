namespace App.Crawler;

public interface ICrawlerClient
{
    Task<string?> GetPage(Uri targetUrl, CancellationToken cancellationToken = default);
}
