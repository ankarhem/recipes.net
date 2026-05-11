using Schema.NET;

namespace App.Crawler;

public interface IScraperService
{
    Task<IReadOnlyList<Uri>> ExtractLinksAsync(
        string html,
        Uri baseUrl,
        CancellationToken cancellationToken = default
    );

    Recipe? ExtractRecipe(string html);
}
