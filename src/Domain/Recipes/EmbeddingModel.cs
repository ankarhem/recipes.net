namespace Domain.Recipes;

public abstract record EmbeddingModel
{
    public abstract string ProviderId { get; }
    public abstract int Dimensions { get; }

    public sealed record TextEmbedding3Small : EmbeddingModel
    {
        public static readonly TextEmbedding3Small Instance = new();
        public override string ProviderId => "text-embedding-3-small";
        public override int Dimensions => 1536;
    }
}
