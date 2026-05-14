namespace Domain.User;

public sealed class User
{
    private readonly List<EmailVerificationToken> _emailVerificationTokens = [];
    private readonly List<PasswordResetToken> _passwordResetTokens = [];

    public UserId Id { get; private set; }
    public Email Email { get; private set; } = null!;
    public PasswordHash PasswordHash { get; private set; } = null!;
    public bool EmailVerified { get; private set; }
    public DateTimeOffset? EmailVerifiedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyCollection<EmailVerificationToken> EmailVerificationTokens =>
        _emailVerificationTokens.AsReadOnly();

    public IReadOnlyCollection<PasswordResetToken> PasswordResetTokens =>
        _passwordResetTokens.AsReadOnly();

    private User() { }

    public static User Register(Email email, PasswordHash passwordHash, IClock clock)
    {
        var now = clock.UtcNow;
        return new User
        {
            Id = UserId.New(),
            Email = email,
            PasswordHash = passwordHash,
            EmailVerified = false,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    public EmailVerificationToken IssueEmailVerificationToken(
        TokenHash hash,
        DateTimeOffset expiresAt,
        IClock clock
    )
    {
        _emailVerificationTokens.RemoveAll(t => !t.IsConsumed);
        var token = EmailVerificationToken.Issue(Id, hash, expiresAt, clock);
        _emailVerificationTokens.Add(token);
        UpdatedAt = clock.UtcNow;
        return token;
    }

    public bool VerifyEmail(TokenHash hash, IClock clock)
    {
        var now = clock.UtcNow;
        var token = _emailVerificationTokens.SingleOrDefault(t => t.TokenHash == hash);

        if (token is null || token.IsConsumed || token.IsExpired(now))
        {
            return false;
        }

        token.Consume(clock);
        EmailVerified = true;
        EmailVerifiedAt = now;
        UpdatedAt = now;
        return true;
    }

    public PasswordResetToken IssuePasswordResetToken(
        TokenHash hash,
        DateTimeOffset expiresAt,
        IClock clock
    )
    {
        _passwordResetTokens.RemoveAll(t => !t.IsConsumed);
        var token = PasswordResetToken.Issue(Id, hash, expiresAt, clock);
        _passwordResetTokens.Add(token);
        UpdatedAt = clock.UtcNow;
        return token;
    }

    public bool ResetPassword(TokenHash resetTokenHash, PasswordHash newPasswordHash, IClock clock)
    {
        var now = clock.UtcNow;
        var token = _passwordResetTokens.SingleOrDefault(t => t.TokenHash == resetTokenHash);

        if (token is null || token.IsConsumed || token.IsExpired(now))
        {
            return false;
        }

        token.Consume(clock);
        PasswordHash = newPasswordHash;
        UpdatedAt = now;
        return true;
    }
}
