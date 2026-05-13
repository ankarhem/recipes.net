namespace App.Auth;

public sealed class AuthService(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IAccessTokenService accessTokenService,
    IRefreshTokenRepository refreshTokenRepository,
    IRefreshTokenGenerator refreshTokenGenerator
) : IAuthService
{
    private const int RefreshTokenDays = 7;

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
        var accessToken = accessTokenService.Generate(user.Id, user.Email);
        var refreshToken = await StoreRefreshTokenAsync(user.Id, cancellationToken);

        return new AuthResult.Success(user.Id, user.Email, accessToken, refreshToken);
    }

    public async Task<AuthResult> LoginAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default
    )
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var user = await userRepository.GetByEmailAsync(normalizedEmail, cancellationToken);

        if (user is null || !passwordHasher.Verify(password, user.PasswordHash))
        {
            return new AuthResult.InvalidCredentials();
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

        await refreshTokenRepository.RevokeAsync(stored.Id, cancellationToken);

        var user = await userRepository.GetByIdAsync(stored.UserId, cancellationToken);
        if (user is null)
        {
            return new AuthResult.InvalidRefreshToken();
        }

        var accessToken = accessTokenService.Generate(user.Id, user.Email);
        var newRefreshToken = await StoreRefreshTokenAsync(user.Id, cancellationToken);

        return new AuthResult.Success(user.Id, user.Email, accessToken, newRefreshToken);
    }

    private async Task<string> StoreRefreshTokenAsync(
        Guid userId,
        CancellationToken cancellationToken
    )
    {
        var (plainToken, hashedToken) = refreshTokenGenerator.Generate();
        await refreshTokenRepository.StoreAsync(
            userId,
            hashedToken,
            DateTimeOffset.UtcNow.AddDays(RefreshTokenDays),
            cancellationToken
        );

        return plainToken;
    }
}
