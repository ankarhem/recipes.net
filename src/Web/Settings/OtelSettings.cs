namespace Web.Settings;

public sealed class OtelSettings
{
    public string? OtlpEndpoint { get; init; }
    public bool ConsoleExporterEnabled { get; init; }
}
