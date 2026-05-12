namespace App.Recipe;

public sealed record ExtractedPage
{
    public required IReadOnlyList<Uri> Links { get; init; }
    public Domain.Recipe.Recipe? Recipe { get; init; }
    public string? RawJsonLd { get; init; }
}
