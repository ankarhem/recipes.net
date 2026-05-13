using DomainRecipe = Domain.Recipe.Recipe;

namespace App.Embedding;

public interface IRecipeEmbeddingTextBuilder
{
    string Build(DomainRecipe recipe);
}
