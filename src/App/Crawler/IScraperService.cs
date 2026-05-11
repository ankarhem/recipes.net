namespace App.Crawler;

public interface IScraperService
{
    Task<IReadOnlyList<Uri>> ExtractLinksAsync(
        string html,
        Uri baseUrl,
        CancellationToken cancellationToken = default
    );

    string? ExtractRecipeJsonLd(string html);
}
