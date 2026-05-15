namespace Domain.Identity;

public sealed class RecoveryCode
{
    public Guid Id { get; private set; }
    public UserId UserId { get; private set; }
    public RecoveryCodeHash CodeHash { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ConsumedAt { get; private set; }

    public bool IsConsumed => ConsumedAt is not null;

    private RecoveryCode() { }

    public static RecoveryCode Create(UserId userId, RecoveryCodeHash hash, IClock clock) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CodeHash = hash,
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
