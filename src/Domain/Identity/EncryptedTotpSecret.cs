namespace Domain.Identity;

public sealed record EncryptedTotpSecret
{
    public string Value { get; }

    private EncryptedTotpSecret(string value)
    {
        Value = value;
    }

    public static EncryptedTotpSecret From(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Encrypted TOTP secret must not be empty.", nameof(value));
        }

        return new EncryptedTotpSecret(value);
    }

    public override string ToString() => Value;
}
