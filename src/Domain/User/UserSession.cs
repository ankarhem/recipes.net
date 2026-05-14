namespace Domain.User;

public sealed class UserSession
{
    public Guid Id { get; private set; }
    public UserId UserId { get; private set; }
    public TokenHash TokenHash { get; private set; } = null!;
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }

    public bool IsRevoked => RevokedAt is not null;

    public bool IsExpired(DateTimeOffset now) => ExpiresAt < now;

    public bool IsActive(DateTimeOffset now) => !IsRevoked && !IsExpired(now);

    private UserSession() { }

    public static UserSession Issue(
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

    public bool TryRevoke(IClock clock)
    {
        if (IsRevoked)
        {
            return false;
        }

        RevokedAt = clock.UtcNow;
        return true;
    }
}
