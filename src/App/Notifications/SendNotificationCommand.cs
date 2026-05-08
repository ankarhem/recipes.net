using System.Text.Json;

namespace App.Notifications;

public sealed class SendNotificationCommand
{
    public required JsonElement Body { get; init; }
    public required Uri TargetUrl { get; init; }
    public Dictionary<string, string>? Headers { get; init; }
    public TimeSpan? Delay { get; init; }
}
