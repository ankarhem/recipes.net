namespace Domain.Identity;

public sealed class TwoFactorChallenge
{
    public Guid Id { get; private set; }
    public UserId UserId { get; private set; }
    public TokenHash TokenHash { get; private set; } = null!;
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ConsumedAt { get; private set; }

    public bool IsConsumed => ConsumedAt is not null;

    public bool IsExpired(DateTimeOffset now) => ExpiresAt < now;

    public bool IsActive(DateTimeOffset now) => !IsConsumed && !IsExpired(now);

    private TwoFactorChallenge() { }

    public static TwoFactorChallenge Issue(
        UserId userId,
        TokenHash tokenHash,
        DateTimeOffset expiresAt,
        IClock clock
    ) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt,
            CreatedAt = clock.UtcNow,
        };

    public bool TryConsume(IClock clock)
    {
        if (IsConsumed)
        {
            return false;
        }

        ConsumedAt = clock.UtcNow;
        return true;
    }
}
