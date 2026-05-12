using App.Crawler;
using AwesomeAssertions;
using Xunit;

namespace App.UnitTests;

public class WorkflowIdTests
{
    [Fact]
    public void Create_ReturnsIdWithHostPrefix()
    {
        var id = WorkflowId.Create("example.com");

        id.Value.Should().StartWith("example.com-");
        id.Value.Length.Should().Be("example.com-".Length + 10);
    }

    [Fact]
    public void Create_GeneratesUniqueIds()
    {
        var id1 = WorkflowId.Create("example.com");
        var id2 = WorkflowId.Create("example.com");

        id1.Value.Should().NotBe(id2.Value);
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
