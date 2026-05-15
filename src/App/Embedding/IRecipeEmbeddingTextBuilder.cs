
using Domain.Recipes;

namespace App.Embedding;

public interface IRecipeEmbeddingTextBuilder
{
    string Build(Recipe recipe);
}
