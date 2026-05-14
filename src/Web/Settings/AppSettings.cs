namespace Web.Settings;

public sealed class AppSettings
{
    public string ServiceName { get; init; } = "recipes";
    public OtelSettings Otel { get; init; } = new();
    public TemporalSettings Temporal { get; init; } = new();
    public OpenAiSettings OpenAi { get; init; } = new();
    public JwtSettings Jwt { get; init; } = new();
    public EmailSettings Email { get; init; } = new();
}

public sealed class OtelSettings
{
    public string? OtlpEndpoint { get; init; }
    public bool ConsoleExporterEnabled { get; init; }
}

public sealed class TemporalSettings
{
    public string Target { get; init; } = "localhost:7233";
    public string Namespace { get; init; } = "default";
    public string TaskQueue { get; init; } = "recipes";
}

public sealed class OpenAiSettings
{
    public string ApiKey { get; init; } = "";
}

public sealed class JwtSettings
{
    public string Issuer { get; init; } = "recipes";
    public string Audience { get; init; } = "recipes";
    public string SigningKey { get; init; } = "";
    public int AccessTokenMinutes { get; init; } = 15;
    public int RefreshTokenDays { get; init; } = 7;
}

public sealed class EmailSettings
{
    public string SmtpHost { get; init; } = "";
    public int SmtpPort { get; init; } = 587;
    public string SmtpUser { get; init; } = "";
    public string SmtpPass { get; init; } = "";
    public string FromEmail { get; init; } = "noreply@recipes.app";
    public string FromName { get; init; } = "Recipes";
    public string BaseUrl { get; init; } = "http://localhost:3000";
}
