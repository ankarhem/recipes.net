using App.Crawler;
using AwesomeAssertions;
using Xunit;

namespace App.UnitTests;

public class WorkflowIdTests
{
    [Fact]
    public void ForHost_ReturnsIdWithHostAsValue()
    {
        var id = WorkflowId.ForHost("example.com");

        id.Value.Should().Be("example.com");
    }

    [Fact]
    public void ForHost_SameHost_ReturnsIdenticalIds()
    {
        var id1 = WorkflowId.ForHost("example.com");
        var id2 = WorkflowId.ForHost("example.com");

        id1.Value.Should().Be(id2.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ForHost_ThrowsOnEmptyOrNull(string? host)
    {
        var act = () => WorkflowId.ForHost(host!);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_AcceptsValidValue()
    {
        var id = new WorkflowId("example.com-VaK5fP3m9g");

        id.Value.Should().Be("example.com-VaK5fP3m9g");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_ThrowsOnEmptyOrNull(string? value)
    {
        var act = () => new WorkflowId(value!);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_ThrowsOnExceeds255Chars()
    {
        var tooLong = new string('a', 256);

        var act = () => new WorkflowId(tooLong);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_Accepts255Chars()
    {
        var exactly255 = new string('a', 255);

        var id = new WorkflowId(exactly255);

        id.Value.Should().Be(exactly255);
    }

    [Fact]
    public void ToString_ReturnsValue()
    {
        var id = new WorkflowId("example.com-VaK5fP3m9g");

        id.ToString().Should().Be("example.com-VaK5fP3m9g");
    }

    [Fact]
    public void ExplicitCast_ToString()
    {
        var id = new WorkflowId("example.com-VaK5fP3m9g");

        ((string)id).Should().Be("example.com-VaK5fP3m9g");
    }
}
