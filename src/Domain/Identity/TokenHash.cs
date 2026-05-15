namespace Domain.Identity;

public sealed record TokenHash
{
    public string Value { get; }

    private TokenHash(string value)
    {
        Value = value;
    }

    public static TokenHash From(string hexHash)
    {
        if (string.IsNullOrWhiteSpace(hexHash))
        {
            throw new ArgumentException("Token hash must not be empty.", nameof(hexHash));
        }

        return new TokenHash(hexHash);
    }

    public static TokenHash FromPlain(string plainToken) => From(TokenHasher.Hash(plainToken));

    public override string ToString() => Value;
}
