namespace Infrastructure.Identity;

public sealed class JwtAccessTokenOptions
{
    public string Issuer { get; init; } = "recipes";
    public string Audience { get; init; } = "recipes";
    public string SigningKey { get; init; } = "";
    public int AccessTokenMinutes { get; init; } = 15;
}
