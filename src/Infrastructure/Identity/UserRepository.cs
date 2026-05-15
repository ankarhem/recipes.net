using App.Identity;
using Domain.Identity;
using Infrastructure.Recipe;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Identity;

public sealed class UserRepository(RecipesDbContext db) : IUserRepository
{
    public Task<User?> GetByIdAsync(UserId id, CancellationToken cancellationToken = default) =>
        QueryWithAggregate().SingleOrDefaultAsync(u => u.Id == id, cancellationToken);

    public Task<User?> GetByEmailAsync(
        Email email,
        CancellationToken cancellationToken = default
    ) => QueryWithAggregate().SingleOrDefaultAsync(u => u.Email == email, cancellationToken);

    public Task<User?> GetByEmailVerificationTokenHashAsync(
        TokenHash hash,
        CancellationToken cancellationToken = default
    ) =>
        QueryWithAggregate()
            .SingleOrDefaultAsync(
                u => u.EmailVerificationTokens.Any(t => t.TokenHash == hash),
                cancellationToken
            );

    public Task<User?> GetByPasswordResetTokenHashAsync(
        TokenHash hash,
        CancellationToken cancellationToken = default
    ) =>
        QueryWithAggregate()
            .SingleOrDefaultAsync(
                u => u.PasswordResetTokens.Any(t => t.TokenHash == hash),
                cancellationToken
            );

    public Task<User?> GetByTwoFactorChallengeHashAsync(
        TokenHash challengeHash,
        CancellationToken cancellationToken = default
    ) =>
        QueryWithAggregate()
            .SingleOrDefaultAsync(
                u => u.TwoFactorChallenges.Any(c => c.TokenHash == challengeHash),
                cancellationToken
            );

    public async Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        await db.Users.AddAsync(user, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyConflictException(
                "Concurrent modification detected on user aggregate.",
                ex
            );
        }
    }

    private IQueryable<User> QueryWithAggregate() =>
        db.Users
            .Include(u => u.EmailVerificationTokens)
            .Include(u => u.PasswordResetTokens)
            .Include(u => u.Totp)
            .Include(u => u.RecoveryCodes)
            .Include(u => u.TwoFactorChallenges);
}
