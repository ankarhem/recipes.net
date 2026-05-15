namespace App.Recipes;

public enum RecipeEmbeddingModel
{
    TextEmbedding3Small,
}

public static class RecipeEmbeddingModelExtensions
{
    public static int Dimensions(this RecipeEmbeddingModel model) =>
        model switch
        {
            RecipeEmbeddingModel.TextEmbedding3Small => 1536,
            _ => throw new ArgumentOutOfRangeException(
                nameof(model),
                model,
                "Unknown embedding model"
            ),
        };

    public static string OpenAiModelId(this RecipeEmbeddingModel model) =>
        model switch
        {
            RecipeEmbeddingModel.TextEmbedding3Small => "text-embedding-3-small",
            _ => throw new ArgumentOutOfRangeException(
                nameof(model),
                model,
                "Unknown embedding model"
            ),
        };
}
