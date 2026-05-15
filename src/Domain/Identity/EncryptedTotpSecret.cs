namespace Domain.Identity;

public sealed class EncryptedTotpSecret : IEquatable<EncryptedTotpSecret>
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

    public bool Equals(EncryptedTotpSecret? other) => other is not null && Value == other.Value;

    public override bool Equals(object? obj) => obj is EncryptedTotpSecret other && Equals(other);

    public override int GetHashCode() => Value.GetHashCode(StringComparison.Ordinal);

    public override string ToString() => Value;
}
