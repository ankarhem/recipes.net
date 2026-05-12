using Domain;

namespace App.Crawler;

public interface IRecipeRepository
{
    Task SaveAsync(RecipeEntity recipe, CancellationToken cancellationToken = default);
}
