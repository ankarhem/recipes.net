using System.Linq.Expressions;
using App.Crawler;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Temporalio.Client;
using Temporalio.Exceptions;
using Xunit;

namespace App.UnitTests;

public class CrawlerServiceTests
{
    [Fact]
    public async Task StartAsync_DuplicateWorkflow_ThrowsWorkflowAlreadyStartedException()
    {
        var client = Substitute.For<ITemporalClient>();
        client
            .StartWorkflowAsync(
                Arg.Any<Expression<Func<CrawlerWorkflow, Task>>>(),
                Arg.Any<WorkflowOptions>()
            )
            .ReturnsForAnyArgs(
                (Func<NSubstitute.Core.CallInfo, WorkflowHandle<CrawlerWorkflow>>)(
                    _ =>
                        throw new WorkflowAlreadyStartedException(
                            "already started",
                            "example.com",
                            "run123",
                            "CrawlerWorkflow"
                        )
                )
            );

        var sut = new CrawlerService(NullLogger<CrawlerService>.Instance, client, "test-queue");

        var act = () =>
            sut.StartAsync(new StartCrawlJobCommand { TargetUrl = new Uri("https://example.com") });

        await act.Should().ThrowAsync<WorkflowAlreadyStartedException>();
    }
}
