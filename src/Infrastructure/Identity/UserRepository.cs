using App.Identity;
using Domain.Identity;
using Infrastructure.Recipe;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Identity;

public sealed class UserRepository(RecipesDbContext db) : IUserRepository
{
    public Task<User?> GetByIdAsync(UserId id, CancellationToken cancellationToken = default) =>
        QueryWithTokens().SingleOrDefaultAsync(u => u.Id == id, cancellationToken);

    public Task<User?> GetByEmailAsync(
        Email email,
        CancellationToken cancellationToken = default
    ) => QueryWithTokens().SingleOrDefaultAsync(u => u.Email == email, cancellationToken);

    public Task<User?> GetByEmailVerificationTokenHashAsync(
        TokenHash hash,
        CancellationToken cancellationToken = default
    ) =>
        QueryWithTokens()
            .SingleOrDefaultAsync(
                u => u.EmailVerificationTokens.Any(t => t.TokenHash == hash),
                cancellationToken
            );

    public Task<User?> GetByPasswordResetTokenHashAsync(
        TokenHash hash,
        CancellationToken cancellationToken = default
    ) =>
        QueryWithTokens()
            .SingleOrDefaultAsync(
                u => u.PasswordResetTokens.Any(t => t.TokenHash == hash),
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

    private IQueryable<User> QueryWithTokens() =>
        db.Users.Include(u => u.EmailVerificationTokens).Include(u => u.PasswordResetTokens);
}
