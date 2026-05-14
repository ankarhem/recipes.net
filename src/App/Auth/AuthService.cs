namespace App.Auth;

public sealed class AuthService(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IAccessTokenService accessTokenService,
    IRefreshTokenRepository refreshTokenRepository,
    ISecureTokenGenerator secureTokenGenerator,
    IEmailVerificationTokenRepository emailVerificationTokenRepository,
    IPasswordResetTokenRepository passwordResetTokenRepository,
    IEmailWorkflowStarter emailWorkflowStarter
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
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var existing = await userRepository.GetByEmailAsync(normalizedEmail, cancellationToken);
        if (existing is not null)
        {
            return new AuthResult.EmailAlreadyRegistered();
        }

        var hash = passwordHasher.Hash(password);
        var user = await userRepository.CreateAsync(normalizedEmail, hash, cancellationToken);
        var verificationToken = await StoreVerificationTokenAsync(user.Id, cancellationToken);
        await emailWorkflowStarter.StartVerificationWorkflowAsync(
            user.Id,
            user.Email,
            verificationToken,
            cancellationToken
        );

        return new AuthResult.RegistrationPending(user.Id, user.Email);
    }

    public async Task<AuthResult> LoginAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default
    )
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var user = await userRepository.GetByEmailAsync(normalizedEmail, cancellationToken);

        if (user is null)
        {
            _ = passwordHasher.Verify(password, passwordHasher.DummyHash);
            return new AuthResult.InvalidCredentials();
        }

        if (!passwordHasher.Verify(password, user.PasswordHash))
        {
            return new AuthResult.InvalidCredentials();
        }

        if (!user.EmailVerified)
        {
            return new AuthResult.EmailNotVerified();
        }

        var accessToken = accessTokenService.Generate(user.Id, user.Email);
        var refreshToken = await StoreRefreshTokenAsync(user.Id, cancellationToken);

        return new AuthResult.Success(user.Id, user.Email, accessToken, refreshToken);
    }

    public async Task<AuthResult> RefreshAsync(
        string refreshToken,
        CancellationToken cancellationToken = default
    )
    {
        var tokenHash = TokenHasher.Hash(refreshToken);
        var stored = await refreshTokenRepository.FindByTokenHashAsync(tokenHash, cancellationToken);

        if (stored is null || stored.IsExpired || stored.IsRevoked)
        {
            if (stored is not null)
            {
                await refreshTokenRepository.RevokeAllForUserAsync(stored.UserId, cancellationToken);
            }

            return new AuthResult.InvalidRefreshToken();
        }

        var revoked = await refreshTokenRepository.TryRevokeAsync(stored.Id, cancellationToken);
        if (!revoked)
        {
            await refreshTokenRepository.RevokeAllForUserAsync(stored.UserId, cancellationToken);
            return new AuthResult.InvalidRefreshToken();
        }

        var user = await userRepository.GetByIdAsync(stored.UserId, cancellationToken);
        if (user is null)
        {
            return new AuthResult.InvalidRefreshToken();
        }

        if (!user.EmailVerified)
        {
            await refreshTokenRepository.RevokeAllForUserAsync(user.Id, cancellationToken);
            return new AuthResult.EmailNotVerified();
        }

        var accessToken = accessTokenService.Generate(user.Id, user.Email);
        var newRefreshToken = await StoreRefreshTokenAsync(user.Id, cancellationToken);

        return new AuthResult.Success(user.Id, user.Email, accessToken, newRefreshToken);
    }

    public async Task<AuthResult> VerifyEmailAsync(
        string token,
        CancellationToken cancellationToken = default
    )
    {
        var tokenHash = TokenHasher.Hash(token);
        var stored = await emailVerificationTokenRepository.FindByTokenHashAsync(
            tokenHash,
            cancellationToken
        );

        if (stored is null)
        {
            return new AuthResult.InvalidVerificationToken();
        }

        if (stored.IsConsumed)
        {
            return new AuthResult.InvalidVerificationToken();
        }

        if (stored.IsExpired)
        {
            return new AuthResult.VerificationTokenExpired();
        }

        var consumed = await emailVerificationTokenRepository.TryConsumeAsync(
            stored.Id,
            cancellationToken
        );
        if (!consumed)
        {
            return new AuthResult.InvalidVerificationToken();
        }

        await userRepository.MarkEmailVerifiedAsync(stored.UserId, cancellationToken);

        var user = await userRepository.GetByIdAsync(stored.UserId, cancellationToken);
        if (user is null)
        {
            return new AuthResult.InvalidVerificationToken();
        }

        var accessToken = accessTokenService.Generate(user.Id, user.Email);
        var refreshToken = await StoreRefreshTokenAsync(user.Id, cancellationToken);

        return new AuthResult.Success(user.Id, user.Email, accessToken, refreshToken);
    }

    public async Task<AuthResult> ResendVerificationAsync(
        string email,
        CancellationToken cancellationToken = default
    )
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var user = await userRepository.GetByEmailAsync(normalizedEmail, cancellationToken);

        if (user is null || user.EmailVerified)
        {
            return new AuthResult.EmailVerificationSent();
        }

        await emailVerificationTokenRepository.DeleteAllForUserAsync(user.Id, cancellationToken);
        var verificationToken = await StoreVerificationTokenAsync(user.Id, cancellationToken);
        await emailWorkflowStarter.StartVerificationWorkflowAsync(
            user.Id,
            user.Email,
            verificationToken,
            cancellationToken
        );

        return new AuthResult.EmailVerificationSent();
    }

    public async Task<AuthResult> ForgotPasswordAsync(
        string email,
        CancellationToken cancellationToken = default
    )
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var user = await userRepository.GetByEmailAsync(normalizedEmail, cancellationToken);

        if (user is null || !user.EmailVerified)
        {
            return new AuthResult.PasswordResetSent();
        }

        await passwordResetTokenRepository.DeleteAllForUserAsync(user.Id, cancellationToken);
        var resetToken = await StorePasswordResetTokenAsync(user.Id, cancellationToken);
        await emailWorkflowStarter.StartPasswordResetWorkflowAsync(
            user.Id,
            user.Email,
            resetToken,
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
        var tokenHash = TokenHasher.Hash(token);
        var stored = await passwordResetTokenRepository.FindByTokenHashAsync(
            tokenHash,
            cancellationToken
        );

        if (stored is null)
        {
            return new AuthResult.InvalidResetToken();
        }

        if (stored.IsConsumed)
        {
            return new AuthResult.InvalidResetToken();
        }

        if (stored.IsExpired)
        {
            return new AuthResult.ResetTokenExpired();
        }

        var consumed = await passwordResetTokenRepository.TryConsumeAsync(
            stored.Id,
            cancellationToken
        );
        if (!consumed)
        {
            return new AuthResult.InvalidResetToken();
        }

        var newHash = passwordHasher.Hash(newPassword);
        await userRepository.UpdatePasswordHashAsync(stored.UserId, newHash, cancellationToken);

        await refreshTokenRepository.RevokeAllForUserAsync(stored.UserId, cancellationToken);

        var user = await userRepository.GetByIdAsync(stored.UserId, cancellationToken);
        if (user is null)
        {
            return new AuthResult.InvalidResetToken();
        }

        var accessToken = accessTokenService.Generate(user.Id, user.Email);
        var refreshToken = await StoreRefreshTokenAsync(user.Id, cancellationToken);

        return new AuthResult.Success(user.Id, user.Email, accessToken, refreshToken);
    }

    private async Task<string> StoreRefreshTokenAsync(
        Guid userId,
        CancellationToken cancellationToken
    )
    {
        var (plainToken, hashedToken) = secureTokenGenerator.Generate();
        await refreshTokenRepository.StoreAsync(
            userId,
            hashedToken,
            DateTimeOffset.UtcNow.AddDays(RefreshTokenDays),
            cancellationToken
        );

        return plainToken;
    }

    private async Task<string> StoreVerificationTokenAsync(
        Guid userId,
        CancellationToken cancellationToken
    )
    {
        var (plainToken, hashedToken) = secureTokenGenerator.Generate();
        await emailVerificationTokenRepository.StoreAsync(
            userId,
            hashedToken,
            DateTimeOffset.UtcNow.AddHours(EmailVerificationHours),
            cancellationToken
        );

        return plainToken;
    }

    private async Task<string> StorePasswordResetTokenAsync(
        Guid userId,
        CancellationToken cancellationToken
    )
    {
        var (plainToken, hashedToken) = secureTokenGenerator.Generate();
        await passwordResetTokenRepository.StoreAsync(
            userId,
            hashedToken,
            DateTimeOffset.UtcNow.AddMinutes(PasswordResetMinutes),
            cancellationToken
        );

        return plainToken;
    }
}
