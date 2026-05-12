using Schema.NET;

namespace App.Recipe;

public interface IScraperService
{
    Task<IReadOnlyList<Uri>> ExtractLinksAsync(
        string html,
        Uri baseUrl,
        CancellationToken cancellationToken = default
    );

    ExtractedRecipe? ExtractRecipe(string html);
}
