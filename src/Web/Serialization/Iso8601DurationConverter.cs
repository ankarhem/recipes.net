using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Web.Serialization;

public sealed class Iso8601DurationConverter : JsonConverter<TimeSpan>
{
    public override TimeSpan Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    )
    {
        var value =
            reader.GetString()
            ?? throw new JsonException("Expected ISO 8601 duration string, got null.");

        return ParseIso8601Duration(value);
    }

    public override void Write(Utf8JsonWriter writer, TimeSpan value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(FormatIso8601Duration(value));
    }

    internal static TimeSpan ParseIso8601Duration(ReadOnlySpan<char> value)
    {
        if (value.Length < 3 || value[0] != 'P')
        {
            throw new JsonException($"Invalid ISO 8601 duration: '{value}'");
        }

        long totalTicks = 0;

        // Split into date (P) and time (T) parts
        var tIndex = value.IndexOf('T');
        var datePart = tIndex >= 0 ? value[1..tIndex] : value[1..];
        var timePart = tIndex >= 0 ? value[(tIndex + 1)..] : ReadOnlySpan<char>.Empty;

        totalTicks += ParseDurationComponents(datePart, DateMultipliers);
        totalTicks += ParseDurationComponents(timePart, TimeMultipliers);

        return TimeSpan.FromTicks(totalTicks);
    }

    private static long ParseDurationComponents(
        ReadOnlySpan<char> part,
        Dictionary<char, double> multipliers
    )
    {
        var ticks = 0L;
        var numberStart = 0;

        for (var i = 0; i < part.Length; i++)
        {
            if (multipliers.TryGetValue(part[i], out var multiplier))
            {
                var number = double.Parse(part[numberStart..i], CultureInfo.InvariantCulture);
                ticks += (long)(number * multiplier);
                numberStart = i + 1;
            }
        }

        if (numberStart < part.Length)
        {
            throw new JsonException(
                $"Invalid ISO 8601 duration component: '{part[numberStart..]}'"
            );
        }

        return ticks;
    }

    internal static string FormatIso8601Duration(TimeSpan value)
    {
        if (value == TimeSpan.Zero)
        {
            return "PT0S";
        }

        var result = new StringBuilder("P");

        if (value.Days > 0)
        {
            result.Append($"{value.Days}D");
        }

        var hours = value.Hours;
        var minutes = value.Minutes;
        var seconds = value.Seconds;

        if (hours > 0 || minutes > 0 || seconds > 0)
        {
            result.Append('T');
            if (hours > 0)
                result.Append($"{hours}H");
            if (minutes > 0)
                result.Append($"{minutes}M");
            if (seconds > 0)
                result.Append($"{seconds}S");
        }

        return result.ToString();
    }

    private static readonly Dictionary<char, double> DateMultipliers = new()
    {
        ['D'] = TimeSpan.TicksPerDay,
        ['W'] = TimeSpan.TicksPerDay * 7,
        ['Y'] = TimeSpan.TicksPerDay * 365,
    };

    private static readonly Dictionary<char, double> TimeMultipliers = new()
    {
        ['H'] = TimeSpan.TicksPerHour,
        ['M'] = TimeSpan.TicksPerMinute,
        ['S'] = TimeSpan.TicksPerSecond,
    };
}
