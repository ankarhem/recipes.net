using Domain;
using Domain.Identity;

namespace App.Identity;

public sealed class AuthService(
    IUserRepository users,
    IUserSessionRepository userSessions,
    IPasswordHasher passwordHasher,
    IAccessTokenService accessTokenService,
    ISecureTokenGenerator secureTokenGenerator,
    IEmailWorkflowStarter emailWorkflowStarter,
    IClock clock
) : IAuthService
{
    private const int RefreshTokenDays = 7;
    private const int EmailVerificationHours = 24;
    private const int PasswordResetMinutes = 60;

    public async Task<AuthResult> RegisterAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default
    )
    {
        var normalizedEmail = Email.Normalize(email);
        var existing = await users.GetByEmailAsync(normalizedEmail, cancellationToken);
        if (existing is not null)
        {
            return new AuthResult.EmailAlreadyRegistered();
        }

        var passwordHash = PasswordHash.From(passwordHasher.Hash(password));
        var user = User.Register(normalizedEmail, passwordHash, clock);

        var (plainToken, hashedToken) = secureTokenGenerator.Generate();
        var expiresAt = clock.UtcNow.AddHours(EmailVerificationHours);
        user.IssueEmailVerificationToken(TokenHash.From(hashedToken), expiresAt, clock);

        await users.AddAsync(user, cancellationToken);
        await users.SaveChangesAsync(cancellationToken);

        await emailWorkflowStarter.StartVerificationWorkflowAsync(
            user.Id.Value,
            user.Email.Value,
            plainToken,
            cancellationToken
        );

        return new AuthResult.RegistrationPending(user.Id.Value, user.Email.Value);
    }

    public async Task<AuthResult> LoginAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default
    )
    {
        var normalizedEmail = Email.Normalize(email);
        var user = await users.GetByEmailAsync(normalizedEmail, cancellationToken);

        if (user is null)
        {
            _ = passwordHasher.Verify(password, passwordHasher.DummyHash);
            return new AuthResult.InvalidCredentials();
        }

        if (!passwordHasher.Verify(password, user.PasswordHash.Value))
        {
            return new AuthResult.InvalidCredentials();
        }

        if (!user.EmailVerified)
        {
            return new AuthResult.EmailNotVerified();
        }

        var accessToken = accessTokenService.Generate(user.Id.Value, user.Email.Value);
        var refreshToken = await IssueSessionAsync(user.Id, cancellationToken);

        return new AuthResult.Success(user.Id.Value, user.Email.Value, accessToken, refreshToken);
    }

    public async Task<AuthResult> RefreshAsync(
        string refreshToken,
        CancellationToken cancellationToken = default
    )
    {
        var hash = TokenHash.From(TokenHasher.Hash(refreshToken));
        var session = await userSessions.GetByTokenHashAsync(hash, cancellationToken);

        if (session is null || !session.IsActive(clock.UtcNow))
        {
            if (session is not null)
            {
                await userSessions.RevokeAllForUserAsync(session.UserId, cancellationToken);
            }

            return new AuthResult.InvalidRefreshToken();
        }

        if (!session.TryRevoke(clock))
        {
            await userSessions.RevokeAllForUserAsync(session.UserId, cancellationToken);
            return new AuthResult.InvalidRefreshToken();
        }

        try
        {
            await userSessions.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            await userSessions.RevokeAllForUserAsync(session.UserId, cancellationToken);
            return new AuthResult.InvalidRefreshToken();
        }

        var user = await users.GetByIdAsync(session.UserId, cancellationToken);
        if (user is null)
        {
            return new AuthResult.InvalidRefreshToken();
        }

        if (!user.EmailVerified)
        {
            await userSessions.RevokeAllForUserAsync(user.Id, cancellationToken);
            return new AuthResult.EmailNotVerified();
        }

        var accessToken = accessTokenService.Generate(user.Id.Value, user.Email.Value);
        var newRefreshToken = await IssueSessionAsync(user.Id, cancellationToken);

        return new AuthResult.Success(user.Id.Value, user.Email.Value, accessToken, newRefreshToken);
    }

    public async Task<AuthResult> VerifyEmailAsync(
        string token,
        CancellationToken cancellationToken = default
    )
    {
        var hash = TokenHash.From(TokenHasher.Hash(token));
        var user = await users.GetByEmailVerificationTokenHashAsync(hash, cancellationToken);
        if (user is null)
        {
            return new AuthResult.InvalidVerificationToken();
        }

        var stored = user.EmailVerificationTokens.SingleOrDefault(t => t.TokenHash == hash);
        if (stored is null || stored.IsConsumed)
        {
            return new AuthResult.InvalidVerificationToken();
        }

        if (stored.IsExpired(clock.UtcNow))
        {
            return new AuthResult.VerificationTokenExpired();
        }

        if (!user.VerifyEmail(hash, clock))
        {
            return new AuthResult.InvalidVerificationToken();
        }

        try
        {
            await users.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            return new AuthResult.InvalidVerificationToken();
        }

        var accessToken = accessTokenService.Generate(user.Id.Value, user.Email.Value);
        var refreshToken = await IssueSessionAsync(user.Id, cancellationToken);

        return new AuthResult.Success(user.Id.Value, user.Email.Value, accessToken, refreshToken);
    }

    public async Task<AuthResult> ResendVerificationAsync(
        string email,
        CancellationToken cancellationToken = default
    )
    {
        var normalizedEmail = Email.Normalize(email);
        var user = await users.GetByEmailAsync(normalizedEmail, cancellationToken);

        if (user is null || user.EmailVerified)
        {
            return new AuthResult.EmailVerificationSent();
        }

        var (plainToken, hashedToken) = secureTokenGenerator.Generate();
        var expiresAt = clock.UtcNow.AddHours(EmailVerificationHours);
        user.IssueEmailVerificationToken(TokenHash.From(hashedToken), expiresAt, clock);
        await users.SaveChangesAsync(cancellationToken);

        await emailWorkflowStarter.StartVerificationWorkflowAsync(
            user.Id.Value,
            user.Email.Value,
            plainToken,
            cancellationToken
        );

        return new AuthResult.EmailVerificationSent();
    }

    public async Task<AuthResult> ForgotPasswordAsync(
        string email,
        CancellationToken cancellationToken = default
    )
    {
        var normalizedEmail = Email.Normalize(email);
        var user = await users.GetByEmailAsync(normalizedEmail, cancellationToken);

        if (user is null || !user.EmailVerified)
        {
            return new AuthResult.PasswordResetSent();
        }

        var (plainToken, hashedToken) = secureTokenGenerator.Generate();
        var expiresAt = clock.UtcNow.AddMinutes(PasswordResetMinutes);
        user.IssuePasswordResetToken(TokenHash.From(hashedToken), expiresAt, clock);
        await users.SaveChangesAsync(cancellationToken);

        await emailWorkflowStarter.StartPasswordResetWorkflowAsync(
            user.Id.Value,
            user.Email.Value,
            plainToken,
            cancellationToken
        );

        return new AuthResult.PasswordResetSent();
    }

    public async Task<AuthResult> ResetPasswordAsync(
        string token,
        string newPassword,
        CancellationToken cancellationToken = default
    )
    {
        var hash = TokenHash.From(TokenHasher.Hash(token));
        var user = await users.GetByPasswordResetTokenHashAsync(hash, cancellationToken);
        if (user is null)
        {
            return new AuthResult.InvalidResetToken();
        }

        var stored = user.PasswordResetTokens.SingleOrDefault(t => t.TokenHash == hash);
        if (stored is null || stored.IsConsumed)
        {
            return new AuthResult.InvalidResetToken();
        }

        if (stored.IsExpired(clock.UtcNow))
        {
            return new AuthResult.ResetTokenExpired();
        }

        var newPasswordHash = PasswordHash.From(passwordHasher.Hash(newPassword));
        if (!user.ResetPassword(hash, newPasswordHash, clock))
        {
            return new AuthResult.InvalidResetToken();
        }

        try
        {
            await users.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            return new AuthResult.InvalidResetToken();
        }

        await userSessions.RevokeAllForUserAsync(user.Id, cancellationToken);

        var accessToken = accessTokenService.Generate(user.Id.Value, user.Email.Value);
        var refreshToken = await IssueSessionAsync(user.Id, cancellationToken);

        return new AuthResult.Success(user.Id.Value, user.Email.Value, accessToken, refreshToken);
    }

    private async Task<string> IssueSessionAsync(UserId userId, CancellationToken cancellationToken)
    {
        var (plainToken, hashedToken) = secureTokenGenerator.Generate();
        var expiresAt = clock.UtcNow.AddDays(RefreshTokenDays);
        var session = UserSession.Issue(userId, TokenHash.From(hashedToken), expiresAt, clock);
        await userSessions.AddAsync(session, cancellationToken);
        await userSessions.SaveChangesAsync(cancellationToken);
        return plainToken;
    }
}
