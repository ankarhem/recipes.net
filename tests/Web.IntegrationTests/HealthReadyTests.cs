using System.Net;
using AwesomeAssertions;

namespace Web.IntegrationTests;

public class HealthReadyTests(IntegrationTestFixture factory)
    : IClassFixture<IntegrationTestFixture>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task HealthReady_Returns200_WhenTemporalIsReachable()
    {
        var response = await _client.GetAsync("/health/ready");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
