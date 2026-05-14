namespace Domain.User;

public sealed record Email
{
    public string Value { get; }

    private Email(string value)
    {
        Value = value;
    }

    public static Email Normalize(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new ArgumentException("Email must not be empty.", nameof(raw));
        }

        return new Email(raw.Trim().ToLowerInvariant());
    }

    public override string ToString() => Value;
}
