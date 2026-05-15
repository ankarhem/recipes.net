using App.Recipes;

namespace App.Crawler;

public sealed class StartCrawlJobCommand
{
    public required Uri TargetUrl { get; init; }

    public int MaxPages { get; init; } = 500;

    public RecipeEmbeddingModel EmbeddingModel { get; init; } = RecipeEmbeddingModel.TextEmbedding3Small;

    public IReadOnlyList<Uri> Queue { get; init; } = [];

    public IReadOnlyList<Uri> Visited { get; init; } = [];
}
