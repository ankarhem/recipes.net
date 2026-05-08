using System.Text.Json;

namespace Web.Models;

public sealed class NotificationEnvelopeRequest
{
    public required JsonElement Body { get; init; }
    public required NotificationMetadata Metadata { get; init; }
}
