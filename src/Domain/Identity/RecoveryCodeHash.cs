namespace Domain.Identity;

public sealed record RecoveryCodeHash
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

    public override string ToString() => Value;
}
