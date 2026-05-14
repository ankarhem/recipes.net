using App.Identity;
using Infrastructure.Recipe;
using Microsoft.EntityFrameworkCore.Storage;

namespace Infrastructure.Identity;

public sealed class EfUnitOfWork(RecipesDbContext db) : IUnitOfWork
{
    public async Task<IUnitOfWorkScope> BeginAsync(CancellationToken cancellationToken = default)
    {
        var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        return new EfUnitOfWorkScope(transaction);
    }

    private sealed class EfUnitOfWorkScope(IDbContextTransaction transaction) : IUnitOfWorkScope
    {
        private bool _committed;

        public async Task CommitAsync(CancellationToken cancellationToken = default)
        {
            await transaction.CommitAsync(cancellationToken);
            _committed = true;
        }

        public async ValueTask DisposeAsync()
        {
            if (!_committed)
            {
                await transaction.RollbackAsync();
            }
            await transaction.DisposeAsync();
        }
    }
}
