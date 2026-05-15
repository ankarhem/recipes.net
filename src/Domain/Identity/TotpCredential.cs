namespace Domain.Identity;

public sealed class TotpCredential
{
    public Guid Id { get; private set; }
    public UserId UserId { get; private set; }
    public EncryptedTotpSecret EncryptedSecret { get; private set; } = null!;
    public bool IsVerified { get; private set; }
    public long? LastUsedStep { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? VerifiedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private TotpCredential() { }

    public static TotpCredential CreatePending(
        UserId userId,
        EncryptedTotpSecret secret,
        IClock clock
    )
    {
        var now = clock.UtcNow;
        return new TotpCredential
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            EncryptedSecret = secret,
            IsVerified = false,
            LastUsedStep = null,
            CreatedAt = now,
            VerifiedAt = null,
            UpdatedAt = now,
        };
    }

    public bool Confirm(long matchedStep, IClock clock)
    {
        if (IsVerified)
        {
            return false;
        }

        var now = clock.UtcNow;
        IsVerified = true;
        VerifiedAt = now;
        LastUsedStep = matchedStep;
        UpdatedAt = now;
        return true;
    }

    public bool TryAdvanceStep(long matchedStep, IClock clock)
    {
        if (!IsVerified)
        {
            return false;
        }

        if (LastUsedStep is not null && matchedStep <= LastUsedStep)
        {
            return false;
        }

        LastUsedStep = matchedStep;
        UpdatedAt = clock.UtcNow;
        return true;
    }
}
