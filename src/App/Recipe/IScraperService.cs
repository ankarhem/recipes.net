namespace App.Recipe;

public interface IScraperService
{
    Task<ExtractedPage> ExtractPageAsync(
        string html,
        Uri baseUrl,
        CancellationToken cancellationToken = default
    );
}
