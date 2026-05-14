namespace Domain.User;

public sealed record PasswordResetToken
{
    public required Guid Id { get; init; }
    public required Guid UserId { get; init; }
    public required string TokenHash { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? ConsumedAt { get; init; }

    public bool IsConsumed => ConsumedAt is not null;
    public bool IsExpired => ExpiresAt < DateTimeOffset.UtcNow;
}
