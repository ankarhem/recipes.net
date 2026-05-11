namespace Web.Settings;

public sealed class AppSettings
{
    public string ServiceName { get; init; } = "recipes";
    public OtelSettings Otel { get; init; } = new();
    public TemporalSettings Temporal { get; init; } = new();
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
    public string TaskQueue { get; init; } = "notifications";
}
