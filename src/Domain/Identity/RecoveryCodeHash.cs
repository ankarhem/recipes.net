namespace Domain.Identity;

public sealed class RecoveryCodeHash : IEquatable<RecoveryCodeHash>
{
    public string Value { get; }

    private RecoveryCodeHash(string value)
    {
        Value = value;
    }

    public static RecoveryCodeHash From(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Recovery code hash must not be empty.", nameof(value));
        }

        return new RecoveryCodeHash(value);
    }

    public bool Equals(RecoveryCodeHash? other) => other is not null && Value == other.Value;

    public override bool Equals(object? obj) => obj is RecoveryCodeHash other && Equals(other);

    public override int GetHashCode() => Value.GetHashCode(StringComparison.Ordinal);

    public override string ToString() => Value;
}
