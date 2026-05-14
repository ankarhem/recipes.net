using App.Auth;
using Domain.User;
using Infrastructure.Recipe;
using Microsoft.EntityFrameworkCore;
using DomainUser = Domain.User.User;

namespace Infrastructure.User;

public sealed class UserRepository(RecipesDbContext db) : IUserRepository
{
    public Task<DomainUser?> GetByIdAsync(UserId id, CancellationToken cancellationToken = default) =>
        QueryWithTokens().SingleOrDefaultAsync(u => u.Id == id, cancellationToken);

    public Task<DomainUser?> GetByEmailAsync(
        Email email,
        CancellationToken cancellationToken = default
    ) => QueryWithTokens().SingleOrDefaultAsync(u => u.Email == email, cancellationToken);

    public Task<DomainUser?> GetByEmailVerificationTokenHashAsync(
        TokenHash hash,
        CancellationToken cancellationToken = default
    ) =>
        QueryWithTokens()
            .SingleOrDefaultAsync(
                u => u.EmailVerificationTokens.Any(t => t.TokenHash == hash),
                cancellationToken
            );

    public Task<DomainUser?> GetByPasswordResetTokenHashAsync(
        TokenHash hash,
        CancellationToken cancellationToken = default
    ) =>
        QueryWithTokens()
            .SingleOrDefaultAsync(
                u => u.PasswordResetTokens.Any(t => t.TokenHash == hash),
                cancellationToken
            );

    public async Task AddAsync(DomainUser user, CancellationToken cancellationToken = default)
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

    private IQueryable<DomainUser> QueryWithTokens() =>
        db.Users.Include(u => u.EmailVerificationTokens).Include(u => u.PasswordResetTokens);
}
