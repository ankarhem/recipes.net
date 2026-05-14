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
    Task MarkEmailVerifiedAsync(Guid userId, CancellationToken cancellationToken = default);
    Task UpdatePasswordHashAsync(
        Guid userId,
        string newPasswordHash,
        CancellationToken cancellationToken = default
    );
}
