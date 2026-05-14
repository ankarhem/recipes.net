namespace Web.Settings;

public sealed class JwtSettings
{
    public string Issuer { get; init; } = "recipes";
    public string Audience { get; init; } = "recipes";
    public string SigningKey { get; init; } = "";
    public int AccessTokenMinutes { get; init; } = 15;
    public int RefreshTokenDays { get; init; } = 7;
}
