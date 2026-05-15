namespace Infrastructure.Identity;

public sealed class TotpOptions
{
    public const string SectionName = "Totp";

    public string Issuer { get; set; } = "recipes";
    public int DriftWindow { get; set; } = 1;
    public int DigitCount { get; set; } = 6;
    public int StepSeconds { get; set; } = 30;
}
