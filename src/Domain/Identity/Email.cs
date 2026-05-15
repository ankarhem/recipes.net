using System.Text.RegularExpressions;

namespace Domain.Identity;

public sealed record Email
{
    private static readonly Regex EmailFormat = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant
    );

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

        var normalized = raw.Trim().ToLowerInvariant();

        if (!EmailFormat.IsMatch(normalized))
        {
            throw new ArgumentException($"Invalid email format: '{normalized}'.", nameof(raw));
        }

        return new Email(normalized);
    }

    public override string ToString() => Value;
}
