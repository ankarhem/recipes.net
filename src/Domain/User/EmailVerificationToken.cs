namespace Domain.User;

public sealed class EmailVerificationToken
{
    public Guid Id { get; private set; }
    public UserId UserId { get; private set; }
    public TokenHash TokenHash { get; private set; } = null!;
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ConsumedAt { get; private set; }

    public bool IsConsumed => ConsumedAt is not null;

    public bool IsExpired(DateTimeOffset now) => ExpiresAt < now;

    private EmailVerificationToken() { }

    public static EmailVerificationToken Issue(
        UserId userId,
        TokenHash hash,
        DateTimeOffset expiresAt,
        IClock clock
    ) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = hash,
            ExpiresAt = expiresAt,
            CreatedAt = clock.UtcNow,
        };

    internal void Consume(IClock clock)
    {
        ConsumedAt = clock.UtcNow;
    }
}
