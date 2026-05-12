using System.Net;
using App.Crawler;
using AwesomeAssertions;
using Infrastructure.Crawler;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace App.WorkflowTests;

public class CrawlerClientTests
{
    private static CrawlerClient CreateClient(HttpMessageHandler handler) =>
        new(new HttpClient(handler), NullLogger<CrawlerClient>.Instance);

    [Fact]
    public async Task GetPageAsync_Success_ReturnsContent()
    {
        using var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, "<html>hello</html>");
        var client = CreateClient(handler);

        var result = await client.GetPageAsync(new Uri("https://example.com"));

        result.Should().Be("<html>hello</html>");
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task GetPageAsync_Non2xx_ThrowsHttpRequestException(HttpStatusCode statusCode)
    {
        using var handler = new FakeHttpMessageHandler(statusCode, "error body");
        var client = CreateClient(handler);

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.GetPageAsync(new Uri("https://example.com"))
        );

        ex.StatusCode.Should().Be(statusCode);
        ex.Message.Should().Contain($"HTTP {(int)statusCode}");
    }

    [Fact]
    public async Task GetPageAsync_Non2xx_IncludesBodyInException()
    {
        using var handler = new FakeHttpMessageHandler(
            HttpStatusCode.InternalServerError,
            "server error details"
        );
        var client = CreateClient(handler);

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.GetPageAsync(new Uri("https://example.com"))
        );

        ex.Message.Should().Contain("server error details");
    }

    [Fact]
    public async Task GetPageAsync_LongErrorBody_TruncatesTo2048Chars()
    {
        var longBody = new string('x', 3000);
        using var handler = new FakeHttpMessageHandler(
            HttpStatusCode.InternalServerError,
            longBody
        );
        var client = CreateClient(handler);

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.GetPageAsync(new Uri("https://example.com"))
        );

        var prefix = $"HTTP {(int)HttpStatusCode.InternalServerError}: ";
        var bodyInMessage = ex.Message[prefix.Length..];
        bodyInMessage.Length.Should().Be(2048);
    }

    [Fact]
    public async Task GetPageAsync_Empty200_ReturnsEmptyString()
    {
        using var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, "");
        var client = CreateClient(handler);

        var result = await client.GetPageAsync(new Uri("https://example.com"));

        result.Should().BeEmpty();
    }

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string? _content;

        public FakeHttpMessageHandler(HttpStatusCode statusCode, string? content)
        {
            _statusCode = statusCode;
            _content = content;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            var response = new HttpResponseMessage(_statusCode);
            if (_content is not null)
            {
                response.Content = new StringContent(_content);
            }

            return Task.FromResult(response);
        }
    }
}
