namespace Domain.Identity;

public sealed class User
{
    private readonly List<EmailVerificationToken> _emailVerificationTokens = [];
    private readonly List<PasswordResetToken> _passwordResetTokens = [];
    private readonly List<RecoveryCode> _recoveryCodes = [];
    private readonly List<TwoFactorChallenge> _twoFactorChallenges = [];

    public UserId Id { get; private set; }
    public Email Email { get; private set; } = null!;
    public PasswordHash PasswordHash { get; private set; } = null!;
    public bool EmailVerified { get; private set; }
    public DateTimeOffset? EmailVerifiedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public TotpCredential? Totp { get; private set; }

    public bool HasTwoFactorEnabled => Totp is { IsVerified: true };

    public IReadOnlyCollection<EmailVerificationToken> EmailVerificationTokens =>
        _emailVerificationTokens.AsReadOnly();

    public IReadOnlyCollection<PasswordResetToken> PasswordResetTokens =>
        _passwordResetTokens.AsReadOnly();

    public IReadOnlyCollection<RecoveryCode> RecoveryCodes => _recoveryCodes.AsReadOnly();

    public IReadOnlyCollection<TwoFactorChallenge> TwoFactorChallenges =>
        _twoFactorChallenges.AsReadOnly();

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

    public void StartTwoFactorSetup(EncryptedTotpSecret secret, IClock clock)
    {
        if (HasTwoFactorEnabled)
        {
            throw new InvalidOperationException("Two-factor authentication is already enabled.");
        }

        Totp = TotpCredential.CreatePending(Id, secret, clock);
        UpdatedAt = clock.UtcNow;
    }

    public bool ConfirmTwoFactor(
        long matchedStep,
        IReadOnlyList<RecoveryCodeHash> codeHashes,
        IClock clock
    )
    {
        if (Totp is null)
        {
            return false;
        }

        if (!Totp.Confirm(matchedStep, clock))
        {
            return false;
        }

        _recoveryCodes.Clear();
        foreach (var codeHash in codeHashes)
        {
            _recoveryCodes.Add(RecoveryCode.Create(Id, codeHash, clock));
        }

        UpdatedAt = clock.UtcNow;
        return true;
    }

    public void IssueTwoFactorChallenge(TokenHash tokenHash, DateTimeOffset expiresAt, IClock clock)
    {
        _twoFactorChallenges.RemoveAll(t => !t.IsConsumed);
        _twoFactorChallenges.Add(TwoFactorChallenge.Issue(Id, tokenHash, expiresAt, clock));
        UpdatedAt = clock.UtcNow;
    }

    public bool ConsumeTwoFactorChallenge(TokenHash tokenHash, IClock clock)
    {
        var now = clock.UtcNow;
        var challenge = _twoFactorChallenges.SingleOrDefault(t => t.TokenHash == tokenHash);

        if (challenge is null || !challenge.IsActive(now))
        {
            return false;
        }

        if (!challenge.TryConsume(clock))
        {
            return false;
        }

        UpdatedAt = clock.UtcNow;
        return true;
    }

    public bool VerifyAndAdvanceTotp(long matchedStep, IClock clock)
    {
        if (Totp is null)
        {
            return false;
        }

        if (!Totp.TryAdvanceStep(matchedStep, clock))
        {
            return false;
        }

        UpdatedAt = clock.UtcNow;
        return true;
    }

    public bool ConsumeRecoveryCode(Guid recoveryCodeId, IClock clock)
    {
        var code = _recoveryCodes.SingleOrDefault(c => c.Id == recoveryCodeId);

        if (code is null)
        {
            return false;
        }

        if (!code.TryConsume(clock))
        {
            return false;
        }

        UpdatedAt = clock.UtcNow;
        return true;
    }

    public void DisableTwoFactor(IClock clock)
    {
        if (!HasTwoFactorEnabled)
        {
            throw new InvalidOperationException("Two-factor authentication is not enabled.");
        }

        Totp = null;
        _recoveryCodes.Clear();
        UpdatedAt = clock.UtcNow;
    }

    public void RegenerateRecoveryCodes(IReadOnlyList<RecoveryCodeHash> newCodeHashes, IClock clock)
    {
        if (!HasTwoFactorEnabled)
        {
            throw new InvalidOperationException("Two-factor authentication is not enabled.");
        }

        _recoveryCodes.Clear();
        foreach (var codeHash in newCodeHashes)
        {
            _recoveryCodes.Add(RecoveryCode.Create(Id, codeHash, clock));
        }

        UpdatedAt = clock.UtcNow;
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

    public bool RemoveEmailVerificationToken(TokenHash hash, IClock clock)
    {
        var removed = _emailVerificationTokens.RemoveAll(t => t.TokenHash == hash);
        if (removed == 0)
        {
            return false;
        }

        UpdatedAt = clock.UtcNow;
        return true;
    }

    public bool RemovePasswordResetToken(TokenHash hash, IClock clock)
    {
        var removed = _passwordResetTokens.RemoveAll(t => t.TokenHash == hash);
        if (removed == 0)
        {
            return false;
        }

        UpdatedAt = clock.UtcNow;
        return true;
    }

    public bool RemoveTwoFactorChallenge(TokenHash tokenHash, IClock clock)
    {
        var removed = _twoFactorChallenges.RemoveAll(t => t.TokenHash == tokenHash);
        if (removed == 0)
        {
            return false;
        }

        UpdatedAt = clock.UtcNow;
        return true;
    }
}
