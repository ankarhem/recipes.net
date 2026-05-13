using DomainUser = Domain.User.User;

namespace App.Auth;

public interface IUserRepository
{
    Task<DomainUser?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<DomainUser> CreateAsync(
        string email,
        string passwordHash,
        CancellationToken cancellationToken = default
    );
    Task<DomainUser?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
