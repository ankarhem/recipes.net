namespace Domain.User;

public sealed record User
{
    public required Guid Id { get; init; }
    public required string Email { get; init; }
    public required string PasswordHash { get; init; }
    public bool EmailVerified { get; init; }
    public DateTimeOffset? EmailVerifiedAt { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }
}
