using Domain.Identity;
using DomainUser = Domain.Identity.User;

namespace App.Identity;

public interface IUserRepository
{
    Task<DomainUser?> GetByIdAsync(UserId id, CancellationToken cancellationToken = default);
    Task<DomainUser?> GetByEmailAsync(Email email, CancellationToken cancellationToken = default);

    Task<DomainUser?> GetByEmailVerificationTokenHashAsync(
        TokenHash hash,
        CancellationToken cancellationToken = default
    );

    Task<DomainUser?> GetByPasswordResetTokenHashAsync(
        TokenHash hash,
        CancellationToken cancellationToken = default
    );

    Task AddAsync(DomainUser user, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
