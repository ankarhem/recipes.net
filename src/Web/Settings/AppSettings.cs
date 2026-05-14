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
