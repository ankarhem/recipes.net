using DomainRefreshToken = Domain.User.RefreshToken;

namespace Infrastructure.User;

public sealed class RefreshTokenEntity
{
    public Guid Id { get; set; }
    public required Guid UserId { get; set; }
    public required string TokenHash { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public UserEntity User { get; set; } = null!;

    public DomainRefreshToken ToDomain() =>
        new()
        {
            Id = Id,
            UserId = UserId,
            TokenHash = TokenHash,
            ExpiresAt = ExpiresAt,
            CreatedAt = CreatedAt,
            RevokedAt = RevokedAt,
        };
}
