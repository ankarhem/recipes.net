namespace App;

public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    Task<IUnitOfWorkScope> BeginAsync(CancellationToken cancellationToken = default);
}

public interface IUnitOfWorkScope : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);
}
