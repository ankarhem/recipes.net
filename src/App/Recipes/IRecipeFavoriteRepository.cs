
using Domain.Recipes;

namespace App.Recipes;

public interface IRecipeFavoriteRepository
{
    Task<RecipeFavorite?> FindAsync(
        Guid userId,
        Guid recipeId,
        CancellationToken cancellationToken = default
    );
    Task AddAsync(Guid userId, Guid recipeId, CancellationToken cancellationToken = default);
    Task RemoveAsync(Guid userId, Guid recipeId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Guid>> ListByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default
    );
}
