namespace App.Recipes;

public sealed record ExtractedPage
{
    public required IReadOnlyList<Uri> Links { get; init; }
    public Domain.Recipes.Recipe? Recipe { get; init; }
    public string? RawJsonLd { get; init; }
}
