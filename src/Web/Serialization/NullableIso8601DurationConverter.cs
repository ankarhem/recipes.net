using System.Text.Json;
using System.Text.Json.Serialization;

namespace Web.Serialization;

public sealed class NullableIso8601DurationConverter : JsonConverter<TimeSpan?>
{
    public override TimeSpan? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    )
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        var value =
            reader.GetString()
            ?? throw new JsonException("Expected ISO 8601 duration string, got null.");

        return Iso8601DurationConverter.ParseIso8601Duration(value);
    }

    public override void Write(
        Utf8JsonWriter writer,
        TimeSpan? value,
        JsonSerializerOptions options
    )
    {
        if (value is null)
        {
            writer.WriteNullValue();
        }
        else
        {
            writer.WriteStringValue(Iso8601DurationConverter.FormatIso8601Duration(value.Value));
        }
    }
}
