namespace Infrastructure.Auth;

public sealed class EmailSettings
{
    public string SmtpHost { get; init; } = "";
    public int SmtpPort { get; init; } = 587;
    public string SmtpUser { get; init; } = "";
    public string SmtpPass { get; init; } = "";
    public string FromEmail { get; init; } = "noreply@recipes.app";
    public string FromName { get; init; } = "Recipes";
    public string BaseUrl { get; init; } = "http://localhost:3000";
    public bool RequireTls { get; init; } = true;
}
