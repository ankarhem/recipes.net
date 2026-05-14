using App.Auth;
using Infrastructure.Recipe;
using Microsoft.EntityFrameworkCore;
using DomainUser = Domain.User.User;

namespace Infrastructure.User;

public sealed class UserRepository(RecipesDbContext db) : IUserRepository
{
    public async Task<DomainUser?> GetByEmailAsync(
        string email,
        CancellationToken cancellationToken = default
    )
    {
        var entity = await db.Users.AsNoTracking()
            .SingleOrDefaultAsync(u => u.Email == email, cancellationToken);

        return entity?.ToDomain();
    }

    public async Task<DomainUser> CreateAsync(
        string email,
        string passwordHash,
        CancellationToken cancellationToken = default
    )
    {
        var now = DateTimeOffset.UtcNow;
        var entity = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = passwordHash,
            EmailVerified = false,
            CreatedAt = now,
            UpdatedAt = now,
        };

        await db.Users.AddAsync(entity, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return entity.ToDomain();
    }

    public async Task<DomainUser?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await db.Users.AsNoTracking()
            .SingleOrDefaultAsync(u => u.Id == id, cancellationToken);

        return entity?.ToDomain();
    }

    public async Task MarkEmailVerifiedAsync(
        Guid userId,
        CancellationToken cancellationToken = default
    )
    {
        await db.Users
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(
                setter => setter
                    .SetProperty(u => u.EmailVerified, true)
                    .SetProperty(u => u.EmailVerifiedAt, DateTimeOffset.UtcNow)
                    .SetProperty(u => u.UpdatedAt, DateTimeOffset.UtcNow),
                cancellationToken
            );
    }

    public async Task UpdatePasswordHashAsync(
        Guid userId,
        string newPasswordHash,
        CancellationToken cancellationToken = default
    )
    {
        await db.Users
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(
                setter => setter
                    .SetProperty(u => u.PasswordHash, newPasswordHash)
                    .SetProperty(u => u.UpdatedAt, DateTimeOffset.UtcNow),
                cancellationToken
            );
    }
}
