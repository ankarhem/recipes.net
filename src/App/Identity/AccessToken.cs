namespace App.Identity;

public sealed record AccessToken
{
    public required string Token { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }
}
