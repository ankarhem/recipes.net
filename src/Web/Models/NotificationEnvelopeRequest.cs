using System.Text.Json;

namespace Web.Models;

public sealed class NotificationEnvelopeRequest
{
    public string? NotificationId { get; init; }
    public required JsonElement Body { get; init; }
    public required NotificationMetadata Metadata { get; init; }
}
