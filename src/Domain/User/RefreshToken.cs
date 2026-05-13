namespace Domain.User;

public sealed record RefreshToken
{
    public required Guid Id { get; init; }
    public required Guid UserId { get; init; }
    public required string TokenHash { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? RevokedAt { get; init; }

    public bool IsRevoked => RevokedAt is not null;
    public bool IsExpired => ExpiresAt < DateTimeOffset.UtcNow;
}
