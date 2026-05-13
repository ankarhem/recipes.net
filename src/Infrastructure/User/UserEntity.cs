using DomainUser = Domain.User.User;

namespace Infrastructure.User;

public sealed class UserEntity
{
    public Guid Id { get; set; }
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public List<RecipeFavoriteEntity> FavoriteEntities { get; set; } = [];
    public List<RefreshTokenEntity> RefreshTokenEntities { get; set; } = [];

    public DomainUser ToDomain() =>
        new()
        {
            Id = Id,
            Email = Email,
            PasswordHash = PasswordHash,
            CreatedAt = CreatedAt,
            UpdatedAt = UpdatedAt,
        };
}
