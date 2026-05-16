using Domain.Identity;

namespace App.Identity.Ports;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(UserId id, CancellationToken cancellationToken = default);
    Task<User?> GetByEmailAsync(Email email, CancellationToken cancellationToken = default);

    Task<User?> GetByEmailVerificationTokenHashAsync(
        TokenHash hash,
        CancellationToken cancellationToken = default
    );

    Task<User?> GetByPasswordResetTokenHashAsync(
        TokenHash hash,
        CancellationToken cancellationToken = default
    );

    Task<User?> GetByTwoFactorChallengeHashAsync(
        TokenHash challengeHash,
        CancellationToken cancellationToken = default
    );

    Task AddAsync(User user, CancellationToken cancellationToken = default);
}
