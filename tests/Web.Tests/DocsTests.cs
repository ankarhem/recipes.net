using System.Net;
using AwesomeAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Web.Tests;

public class DocsTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task OpenApiJson_Returns200()
    {
        var response = await _client.GetAsync("/openapi/v1/openapi.json");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
    }

    [Fact]
    public async Task OpenApiJson_ContainsHealthcheckEndpoint()
    {
        var response = await _client.GetAsync("/openapi/v1/openapi.json");
        var json = await response.Content.ReadAsStringAsync();

        json.Should().Contain("/healthcheck");
    }

    [Fact]
    public async Task ScalarDocs_Returns200()
    {
        var response = await _client.GetAsync("/docs/v1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ScalarDocs_ContainsScalarHtml()
    {
        var response = await _client.GetAsync("/docs/v1");
        var html = await response.Content.ReadAsStringAsync();

        html.Should().Contain("scalar");
    }
}
