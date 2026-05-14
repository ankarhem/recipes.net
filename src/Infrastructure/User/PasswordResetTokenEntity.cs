using DomainPasswordResetToken = Domain.User.PasswordResetToken;

namespace Infrastructure.User;

public sealed class PasswordResetTokenEntity
{
    public Guid Id { get; set; }
    public required Guid UserId { get; set; }
    public required string TokenHash { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
    public UserEntity User { get; set; } = null!;

    public DomainPasswordResetToken ToDomain() =>
        new()
        {
            Id = Id,
            UserId = UserId,
            TokenHash = TokenHash,
            ExpiresAt = ExpiresAt,
            CreatedAt = CreatedAt,
            ConsumedAt = ConsumedAt,
        };
}
