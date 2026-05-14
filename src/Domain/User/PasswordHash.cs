namespace Domain.User;

public sealed record PasswordHash
{
    public string Value { get; }

    private PasswordHash(string value)
    {
        Value = value;
    }

    public static PasswordHash From(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new ArgumentException("Password hash must not be empty.", nameof(raw));
        }

        return new PasswordHash(raw);
    }

    public override string ToString() => Value;
}
