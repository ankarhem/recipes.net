using DomainUser = Domain.User.User;

namespace Infrastructure.User;

public sealed class UserEntity
{
    public Guid Id { get; set; }
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    public bool EmailVerified { get; set; }
    public DateTimeOffset? EmailVerifiedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public List<RecipeFavoriteEntity> FavoriteEntities { get; set; } = [];
    public List<RefreshTokenEntity> RefreshTokenEntities { get; set; } = [];
    public List<EmailVerificationTokenEntity> EmailVerificationTokenEntities { get; set; } = [];
    public List<PasswordResetTokenEntity> PasswordResetTokenEntities { get; set; } = [];

    public DomainUser ToDomain() =>
        new()
        {
            Id = Id,
            Email = Email,
            PasswordHash = PasswordHash,
            EmailVerified = EmailVerified,
            EmailVerifiedAt = EmailVerifiedAt,
            CreatedAt = CreatedAt,
            UpdatedAt = UpdatedAt,
        };
}
