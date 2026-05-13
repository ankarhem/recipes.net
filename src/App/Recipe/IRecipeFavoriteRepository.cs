using DomainRecipeFavorite = Domain.User.RecipeFavorite;

namespace App.Recipe;

public interface IRecipeFavoriteRepository
{
    Task<DomainRecipeFavorite?> FindAsync(
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
