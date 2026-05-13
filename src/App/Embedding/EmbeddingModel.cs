namespace App.Embedding;

public enum EmbeddingModel
{
    TextEmbedding3Small,
}

public static class EmbeddingModelExtensions
{
    public static int Dimensions(this EmbeddingModel model) =>
        model switch
        {
            EmbeddingModel.TextEmbedding3Small => 1536,
            _ => throw new ArgumentOutOfRangeException(
                nameof(model),
                model,
                "Unknown embedding model"
            ),
        };

    public static string OpenAiModelId(this EmbeddingModel model) =>
        model switch
        {
            EmbeddingModel.TextEmbedding3Small => "text-embedding-3-small",
            _ => throw new ArgumentOutOfRangeException(
                nameof(model),
                model,
                "Unknown embedding model"
            ),
        };
}
