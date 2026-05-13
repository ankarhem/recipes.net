using App.Embedding;

namespace App.Crawler;

public sealed class StartCrawlJobCommand
{
    public required Uri TargetUrl { get; init; }
    public EmbeddingModel EmbeddingModel { get; init; } = EmbeddingModel.TextEmbedding3Small;
}
