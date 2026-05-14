namespace App.Identity;

public interface IUnitOfWork
{
    Task<IUnitOfWorkScope> BeginAsync(CancellationToken cancellationToken = default);
}

public interface IUnitOfWorkScope : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);
}
