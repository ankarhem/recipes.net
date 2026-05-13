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
}
