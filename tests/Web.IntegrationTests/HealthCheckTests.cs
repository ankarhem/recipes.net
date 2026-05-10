using System.Net;
using System.Text.Json;
using AwesomeAssertions;

namespace Web.IntegrationTests;

public class HealthCheckTests(IntegrationTestFixture factory)
    : IClassFixture<IntegrationTestFixture>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task HealthCheck_Returns200()
    {
        var response = await _client.GetAsync("/healthcheck");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task HealthCheck_ReturnsCommitHash()
    {
        var response = await _client.GetAsync("/healthcheck");
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);

        doc.RootElement.TryGetProperty("commit", out var commit).Should().BeTrue();
        commit.GetString().Should().NotBeNullOrEmpty();
    }
}
