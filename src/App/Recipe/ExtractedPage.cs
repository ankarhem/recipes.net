namespace App.Recipe;

public sealed record ExtractedPage(
    IReadOnlyList<Uri> Links,
    Domain.Recipe.Recipe? Recipe,
    string? RawJsonLd
);
