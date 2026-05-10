using System.Text.Json;
using AwesomeAssertions;
using Web.Serialization;
using Xunit;

namespace Web.Tests;

public class Iso8601DurationTests
{
    [Theory]
    [InlineData("PT0S", 0, 0, 0)]
    [InlineData("PT30S", 0, 0, 30)]
    [InlineData("PT5M", 0, 5, 0)]
    [InlineData("PT1H", 1, 0, 0)]
    [InlineData("PT1H30M15S", 1, 30, 15)]
    [InlineData("P1D", 0, 0, 0, 1)]
    [InlineData("P1DT2H30M", 2, 30, 0, 1)]
    [InlineData("P2W", 0, 0, 0, 14)]
    public void ParseIso8601Duration_ValidDurations_ParseCorrectly(
        string input,
        int hours,
        int minutes,
        int seconds,
        int days = 0
    )
    {
        var result = Iso8601DurationConverter.ParseIso8601Duration(input);

        result.Should().Be(new TimeSpan(days, hours, minutes, seconds));
    }

    [Theory]
    [InlineData("PT0S")]
    [InlineData("PT30S")]
    [InlineData("PT1H30M15S")]
    [InlineData("P1DT2H")]
    public void FormatIso8601Duration_RoundTripsThroughParse(string input)
    {
        var parsed = Iso8601DurationConverter.ParseIso8601Duration(input);
        var formatted = Iso8601DurationConverter.FormatIso8601Duration(parsed);

        formatted.Should().Be(input);
    }

    [Theory]
    [InlineData("")]
    [InlineData("X")]
    [InlineData("PT")]
    [InlineData("P")]
    public void ParseIso8601Duration_InvalidInput_ThrowsJsonException(string input)
    {
        var act = () => Iso8601DurationConverter.ParseIso8601Duration(input);

        act.Should().Throw<JsonException>();
    }
}
