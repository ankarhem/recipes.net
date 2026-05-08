namespace Web.Models;

public sealed class NotificationMetadata
{
    public required Uri TargetUrl { get; init; }
    public Dictionary<string, string>? Headers { get; init; }
    public TimeSpan? Delay { get; init; }
}
