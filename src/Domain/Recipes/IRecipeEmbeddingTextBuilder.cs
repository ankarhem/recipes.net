namespace Domain.Recipes;

public interface IRecipeEmbeddingTextBuilder
{
    string Build(Recipe recipe);
}
