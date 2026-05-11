namespace App.Crawler;

public interface ICrawlerClient
{
    Task<string> Get(Uri targetUrl, CancellationToken cancellationToken = default);
}
