namespace Web.Settings;

public sealed class AppSettings
{
    public TemporalSettings Temporal { get; init; } = new();
}

public sealed class TemporalSettings
{
    public string Target { get; init; } = "localhost:7233";
    public string Namespace { get; init; } = "default";
    public string TaskQueue { get; init; } = "notifications";
}
