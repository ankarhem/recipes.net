using App.Crawler;
using App.Recipes;
using AwesomeAssertions;
using Infrastructure.Crawler;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Temporalio.Client;
using Temporalio.Exceptions;
using Temporalio.Testing;
using Temporalio.Worker;
using Xunit;

namespace App.WorkflowTests;

public class CrawlerWorkflowTests
{
    private static readonly Uri SeedUrl = new("https://example.com");

    private static readonly string PageWithRecipeHtml = """
        <html><head>
        <script type="application/ld+json">
        {
          "@context": "https://schema.org",
          "@type": "Recipe",
          "name": "Chocolate Cake",
          "recipeIngredient": ["2 cups flour", "1 cup sugar"],
          "recipeInstructions": ["Mix dry ingredients", "Bake at 350F"]
        }
        </script>
        </head><body>recipe page</body></html>
        """;

    private static readonly string PageWithLinksHtml = """
        <html><body>
        <a href="https://example.com/page2">Page 2</a>
        <a href="https://other.com/external">External</a>
        </body></html>
        """;

    private static readonly string PageWithLinksAndRecipeHtml = """
        <html><head>
        <script type="application/ld+json">
        {
          "@context": "https://schema.org",
          "@type": "Recipe",
          "name": "Chocolate Cake",
          "recipeIngredient": ["2 cups flour", "1 cup sugar"],
          "recipeInstructions": ["Mix dry ingredients", "Bake at 350F"]
        }
        </script>
        </head><body>
        <a href="https://example.com/page2">Page 2</a>
        </body></html>
        """;

    private static readonly string EmptyPageHtml = "<html><body>nothing here</body></html>";

    private static string MakeCircularLinksHtml(Uri selfUrl, Uri otherUrl) =>
        $"""
            <html><body>
            <a href="{selfUrl}">Self</a>
            <a href="{otherUrl}">Other</a>
            </body></html>
            """;

    [Fact]
    public async Task RunAsync_SinglePageWithRecipe_SavesRecipe()
    {
        await using var env = await WorkflowEnvironment.StartTimeSkippingAsync();

        var savedRecipes =
            new List<(Domain.Recipes.Recipe Recipe, string SourceUrl, string RawJson)>();

        var client = Substitute.For<ICrawlerClient>();
        client
            .GetPageAsync(SeedUrl, Arg.Any<CancellationToken>())
            .Returns(
                (Func<NSubstitute.Core.CallInfo, Task<string?>>)(
                    _ => Task.FromResult<string?>(PageWithRecipeHtml)
                )
            );

        var repository = Substitute.For<IRecipeRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        repository
            .SaveImportedAsync(
                Arg.Do<Domain.Recipes.Recipe>(r => savedRecipes.Add((r, "", ""))),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(new Domain.Recipes.RecipeId(Guid.NewGuid()));
        unitOfWork.SaveChangesAsync(default).ReturnsForAnyArgs(Task.CompletedTask);

        var activities = new CrawlerActivities(client, CreateScraperService(), repository, unitOfWork, NullLogger<CrawlerActivities>.Instance);

        using var worker = new TemporalWorker(
            env.Client,
            new TemporalWorkerOptions($"tq-{Guid.NewGuid()}")
                .AddWorkflow<CrawlerWorkflow>()
                .AddAllActivities(activities)
                .AddAllActivities(CreateEmbeddingActivities())
        );

        await worker.ExecuteAsync(async () =>
        {
            await env.Client.ExecuteWorkflowAsync(
                (CrawlerWorkflow wf) =>
                    wf.RunAsync(new StartCrawlJobCommand { TargetUrl = SeedUrl }),
                new(id: $"wf-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!)
            );
        });

        savedRecipes.Should().HaveCount(1);
        savedRecipes[0].Recipe.Name.Should().Be("Chocolate Cake");
        savedRecipes[0].Recipe.Ingredients.Should().HaveCount(2);
        savedRecipes[0].Recipe.Instructions.Should().HaveCount(2);
    }

    [Fact]
    public async Task RunAsync_NullPageContent_SkipsProcessing()
    {
        await using var env = await WorkflowEnvironment.StartTimeSkippingAsync();

        var client = Substitute.For<ICrawlerClient>();
        client
            .GetPageAsync(Arg.Any<Uri>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<string?>(null));

        var repository = Substitute.For<IRecipeRepository>();

        var activities = new CrawlerActivities(client, CreateScraperService(), repository, Substitute.For<IUnitOfWork>(), NullLogger<CrawlerActivities>.Instance);

        using var worker = new TemporalWorker(
            env.Client,
            new TemporalWorkerOptions($"tq-{Guid.NewGuid()}")
                .AddWorkflow<CrawlerWorkflow>()
                .AddAllActivities(activities)
                .AddAllActivities(CreateEmbeddingActivities())
        );

        await worker.ExecuteAsync(async () =>
        {
            await env.Client.ExecuteWorkflowAsync(
                (CrawlerWorkflow wf) =>
                    wf.RunAsync(new StartCrawlJobCommand { TargetUrl = SeedUrl }),
                new(id: $"wf-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!)
            );
        });

        await repository
            .DidNotReceive()
            .SaveImportedAsync(
                Arg.Any<Domain.Recipes.Recipe>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task RunAsync_PageWithLinks_CrawlsLinkedPages()
    {
        await using var env = await WorkflowEnvironment.StartTimeSkippingAsync();

        var page2Url = new Uri("https://example.com/page2");
        var fetchedUrls = new List<Uri>();

        var client = Substitute.For<ICrawlerClient>();
        client
            .GetPageAsync(Arg.Do<Uri>(url => fetchedUrls.Add(url)), Arg.Any<CancellationToken>())
            .Returns(
                (Func<NSubstitute.Core.CallInfo, Task<string?>>)(
                    call =>
                    {
                        var url = call.Arg<Uri>();
                        return url == SeedUrl ? Task.FromResult<string?>(PageWithLinksHtml)
                            : url == page2Url ? Task.FromResult<string?>(EmptyPageHtml)
                            : Task.FromResult<string?>(null);
                    }
                )
            );

        var activities = new CrawlerActivities(client, CreateScraperService(), Substitute.For<IRecipeRepository>(), Substitute.For<IUnitOfWork>(), NullLogger<CrawlerActivities>.Instance);

        using var worker = new TemporalWorker(
            env.Client,
            new TemporalWorkerOptions($"tq-{Guid.NewGuid()}")
                .AddWorkflow<CrawlerWorkflow>()
                .AddAllActivities(activities)
                .AddAllActivities(CreateEmbeddingActivities())
        );

        await worker.ExecuteAsync(async () =>
        {
            await env.Client.ExecuteWorkflowAsync(
                (CrawlerWorkflow wf) =>
                    wf.RunAsync(new StartCrawlJobCommand { TargetUrl = SeedUrl }),
                new(id: $"wf-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!)
            );
        });

        fetchedUrls.Should().Contain([SeedUrl, page2Url]);
        // External link should NOT be fetched
        fetchedUrls.Should().NotContain(u => u.Host == "other.com");
    }

    [Fact]
    public async Task RunAsync_DoesNotRevisitUrls()
    {
        await using var env = await WorkflowEnvironment.StartTimeSkippingAsync();

        var circularUrl = new Uri("https://example.com/a");
        var fetchedUrls = new List<Uri>();

        var client = Substitute.For<ICrawlerClient>();
        client
            .GetPageAsync(Arg.Do<Uri>(url => fetchedUrls.Add(url)), Arg.Any<CancellationToken>())
            .Returns(
                (Func<NSubstitute.Core.CallInfo, Task<string?>>)(
                    call =>
                    {
                        var url = call.Arg<Uri>();
                        var html = MakeCircularLinksHtml(SeedUrl, circularUrl);
                        return Task.FromResult<string?>(html);
                    }
                )
            );

        var activities = new CrawlerActivities(client, CreateScraperService(), Substitute.For<IRecipeRepository>(), Substitute.For<IUnitOfWork>(), NullLogger<CrawlerActivities>.Instance);

        using var worker = new TemporalWorker(
            env.Client,
            new TemporalWorkerOptions($"tq-{Guid.NewGuid()}")
                .AddWorkflow<CrawlerWorkflow>()
                .AddAllActivities(activities)
                .AddAllActivities(CreateEmbeddingActivities())
        );

        await worker.ExecuteAsync(async () =>
        {
            await env.Client.ExecuteWorkflowAsync(
                (CrawlerWorkflow wf) =>
                    wf.RunAsync(new StartCrawlJobCommand { TargetUrl = SeedUrl }),
                new(id: $"wf-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!)
            );
        });

        // SeedUrl + circularUrl, but NOT SeedUrl again
        fetchedUrls.Should().BeEquivalentTo([SeedUrl, circularUrl]);
        fetchedUrls.Count(u => u == SeedUrl).Should().Be(1);
        fetchedUrls.Count(u => u == circularUrl).Should().Be(1);
    }

    [Fact]
    public async Task RunAsync_PageWithLinksAndRecipe_SavesRecipeAndCrawlsLinks()
    {
        await using var env = await WorkflowEnvironment.StartTimeSkippingAsync();

        var page2Url = new Uri("https://example.com/page2");
        var savedRecipes = new List<Domain.Recipes.Recipe>();

        var client = Substitute.For<ICrawlerClient>();
        client
            .GetPageAsync(Arg.Any<Uri>(), Arg.Any<CancellationToken>())
            .Returns(
                (Func<NSubstitute.Core.CallInfo, Task<string?>>)(
                    call =>
                    {
                        var url = call.Arg<Uri>();
                        return url == SeedUrl
                            ? Task.FromResult<string?>(PageWithLinksAndRecipeHtml)
                            : Task.FromResult<string?>(EmptyPageHtml);
                    }
                )
            );

        var repository = Substitute.For<IRecipeRepository>();
        repository
            .SaveImportedAsync(
                Arg.Do<Domain.Recipes.Recipe>(r => savedRecipes.Add(r)),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(new Domain.Recipes.RecipeId(Guid.NewGuid()));

        var activities = new CrawlerActivities(client, CreateScraperService(), repository, Substitute.For<IUnitOfWork>(), NullLogger<CrawlerActivities>.Instance);

        using var worker = new TemporalWorker(
            env.Client,
            new TemporalWorkerOptions($"tq-{Guid.NewGuid()}")
                .AddWorkflow<CrawlerWorkflow>()
                .AddAllActivities(activities)
                .AddAllActivities(CreateEmbeddingActivities())
        );

        await worker.ExecuteAsync(async () =>
        {
            await env.Client.ExecuteWorkflowAsync(
                (CrawlerWorkflow wf) =>
                    wf.RunAsync(new StartCrawlJobCommand { TargetUrl = SeedUrl }),
                new(id: $"wf-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!)
            );
        });

        // Recipe extracted from page with both recipe and links
        savedRecipes.Should().HaveCount(1);
        savedRecipes[0].Name.Should().Be("Chocolate Cake");
    }

    [Fact]
    public async Task RunAsync_EmbeddingFailure_LogsAndContinuesCrawl()
    {
        await using var env = await WorkflowEnvironment.StartTimeSkippingAsync();

        var page2Url = new Uri("https://example.com/page2");
        var fetchedUrls = new List<Uri>();
        var savedRecipes = new List<Domain.Recipes.Recipe>();

        var client = Substitute.For<ICrawlerClient>();
        client
            .GetPageAsync(Arg.Do<Uri>(url => fetchedUrls.Add(url)), Arg.Any<CancellationToken>())
            .Returns(
                (Func<NSubstitute.Core.CallInfo, Task<string?>>)(
                    call =>
                    {
                        var url = call.Arg<Uri>();
                        return url == SeedUrl
                            ? Task.FromResult<string?>(PageWithLinksAndRecipeHtml)
                            : Task.FromResult<string?>(EmptyPageHtml);
                    }
                )
            );

        var repository = Substitute.For<IRecipeRepository>();
        repository
            .SaveImportedAsync(
                Arg.Do<Domain.Recipes.Recipe>(r => savedRecipes.Add(r)),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(new Domain.Recipes.RecipeId(Guid.NewGuid()));

        var activities = new CrawlerActivities(client, CreateScraperService(), repository, Substitute.For<IUnitOfWork>(), NullLogger<CrawlerActivities>.Instance);

        using var worker = new TemporalWorker(
            env.Client,
            new TemporalWorkerOptions($"tq-{Guid.NewGuid()}")
                .AddWorkflow<CrawlerWorkflow>()
                .AddAllActivities(activities)
                .AddAllActivities(CreateEmbeddingActivities(new InvalidOperationException("boom")))
        );

        await worker.ExecuteAsync(async () =>
        {
            await env.Client.ExecuteWorkflowAsync(
                (CrawlerWorkflow wf) =>
                    wf.RunAsync(new StartCrawlJobCommand { TargetUrl = SeedUrl }),
                new(id: $"wf-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!)
            );
        });

        savedRecipes.Should().HaveCount(1);
        fetchedUrls.Should().Contain([SeedUrl, page2Url]);
    }

    [Fact]
    public async Task RunAsync_MaxPagesReached_StopsCrawling()
    {
        await using var env = await WorkflowEnvironment.StartTimeSkippingAsync();

        var fetchedUrls = new List<Uri>();
        var client = Substitute.For<ICrawlerClient>();
        client
            .GetPageAsync(Arg.Do<Uri>(url => fetchedUrls.Add(url)), Arg.Any<CancellationToken>())
            .Returns(
                (Func<NSubstitute.Core.CallInfo, Task<string?>>)(
                    _ => Task.FromResult<string?>(PageWithLinksHtml)
                )
            );

        var activities = new CrawlerActivities(client, CreateScraperService(), Substitute.For<IRecipeRepository>(), Substitute.For<IUnitOfWork>(), NullLogger<CrawlerActivities>.Instance);

        using var worker = new TemporalWorker(
            env.Client,
            new TemporalWorkerOptions($"tq-{Guid.NewGuid()}")
                .AddWorkflow<CrawlerWorkflow>()
                .AddAllActivities(activities)
                .AddAllActivities(CreateEmbeddingActivities())
        );

        await worker.ExecuteAsync(async () =>
        {
            await env.Client.ExecuteWorkflowAsync(
                (CrawlerWorkflow wf) =>
                    wf.RunAsync(new StartCrawlJobCommand { TargetUrl = SeedUrl, MaxPages = 1 }),
                new(id: $"wf-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!)
            );
        });

        fetchedUrls.Should().HaveCount(1);
        fetchedUrls.Should().ContainSingle().Which.Should().Be(SeedUrl);
    }

    [Fact]
    public async Task RunAsync_FetchActivityFailure_LogsAndContinues()
    {
        await using var env = await WorkflowEnvironment.StartTimeSkippingAsync();

        var page2Url = new Uri("https://example.com/page2");
        var fetchedUrls = new List<Uri>();

        var client = Substitute.For<ICrawlerClient>();
        client
            .GetPageAsync(Arg.Do<Uri>(url => fetchedUrls.Add(url)), Arg.Any<CancellationToken>())
            .Returns(
                (Func<NSubstitute.Core.CallInfo, Task<string?>>)(
                    call =>
                    {
                        var url = call.Arg<Uri>();
                        if (url == SeedUrl)
                        {
                            return Task.FromResult<string?>(PageWithLinksHtml);
                        }
                        throw new HttpRequestException(
                            $"HTTP 404: not found",
                            null,
                            System.Net.HttpStatusCode.NotFound
                        );
                    }
                )
            );

        var activities = new CrawlerActivities(client, CreateScraperService(), Substitute.For<IRecipeRepository>(), Substitute.For<IUnitOfWork>(), NullLogger<CrawlerActivities>.Instance);

        using var worker = new TemporalWorker(
            env.Client,
            new TemporalWorkerOptions($"tq-{Guid.NewGuid()}")
                .AddWorkflow<CrawlerWorkflow>()
                .AddAllActivities(activities)
                .AddAllActivities(CreateEmbeddingActivities())
        );

        await worker.ExecuteAsync(async () =>
        {
            await env.Client.ExecuteWorkflowAsync(
                (CrawlerWorkflow wf) =>
                    wf.RunAsync(new StartCrawlJobCommand { TargetUrl = SeedUrl }),
                new(id: $"wf-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!)
            );
        });

        fetchedUrls.Should().Contain([SeedUrl, page2Url]);
    }

    [Fact]
    public async Task RunAsync_PauseAndResume_ControlsProcessing()
    {
        await using var env = await WorkflowEnvironment.StartTimeSkippingAsync();

        var fetchCount = 0;
        var client = Substitute.For<ICrawlerClient>();
        client
            .GetPageAsync(Arg.Any<Uri>(), Arg.Any<CancellationToken>())
            .Returns(
                (Func<NSubstitute.Core.CallInfo, Task<string?>>)(
                    call =>
                    {
                        fetchCount++;
                        return Task.FromResult<string?>("""<html><body>no recipe</body></html>""");
                    }
                )
            );

        var activities = new CrawlerActivities(client, CreateScraperService(), Substitute.For<IRecipeRepository>(), Substitute.For<IUnitOfWork>(), NullLogger<CrawlerActivities>.Instance);

        using var worker = new TemporalWorker(
            env.Client,
            new TemporalWorkerOptions($"tq-{Guid.NewGuid()}")
                .AddWorkflow<CrawlerWorkflow>()
                .AddAllActivities(activities)
                .AddAllActivities(CreateEmbeddingActivities())
        );

        await worker.ExecuteAsync(async () =>
        {
            var workflowId = $"wf-{Guid.NewGuid()}";
            var handle = await env.Client.StartWorkflowAsync(
                (CrawlerWorkflow wf) =>
                    wf.RunAsync(new StartCrawlJobCommand { TargetUrl = SeedUrl }),
                new(id: workflowId, taskQueue: worker.Options.TaskQueue!)
            );

            // Signal pause before workflow completes
            await handle.SignalAsync(wf => wf.PauseAsync());
            var state = await handle.QueryAsync(wf => wf.GetState());
            state.UrlsCrawled.Should().BeGreaterThanOrEqualTo(0);
            state.UrlsQueued.Should().BeGreaterThanOrEqualTo(0);
            state.IsPaused.Should().BeTrue();

            // Signal resume
            await handle.SignalAsync(wf => wf.ResumeAsync());
            state = await handle.QueryAsync(wf => wf.GetState());
            state.IsPaused.Should().BeFalse();

            // Let workflow complete
            await handle.GetResultAsync();
        });
    }

    [Fact]
    public async Task RunAsync_GetStatus_ReturnsRunningState()
    {
        await using var env = await WorkflowEnvironment.StartTimeSkippingAsync();

        var client = Substitute.For<ICrawlerClient>();
        client
            .GetPageAsync(Arg.Any<Uri>(), Arg.Any<CancellationToken>())
            .Returns(
                (Func<NSubstitute.Core.CallInfo, Task<string?>>)(
                    _ => Task.FromResult<string?>("<html><body>no recipe</body></html>")
                )
            );

        var activities = new CrawlerActivities(client, CreateScraperService(), Substitute.For<IRecipeRepository>(), Substitute.For<IUnitOfWork>(), NullLogger<CrawlerActivities>.Instance);

        using var worker = new TemporalWorker(
            env.Client,
            new TemporalWorkerOptions($"tq-{Guid.NewGuid()}")
                .AddWorkflow<CrawlerWorkflow>()
                .AddAllActivities(activities)
                .AddAllActivities(CreateEmbeddingActivities())
        );

        await worker.ExecuteAsync(async () =>
        {
            var handle = await env.Client.StartWorkflowAsync(
                (CrawlerWorkflow wf) =>
                    wf.RunAsync(new StartCrawlJobCommand { TargetUrl = SeedUrl }),
                new(id: $"wf-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!)
            );

            await handle.SignalAsync(wf => wf.PauseAsync());

            var state = await handle.QueryAsync(wf => wf.GetState());
            state.UrlsCrawled.Should().BeGreaterThanOrEqualTo(0);
            state.UrlsQueued.Should().BeGreaterThanOrEqualTo(0);
            state.IsPaused.Should().BeTrue();

            await handle.SignalAsync(wf => wf.ResumeAsync());
            await handle.GetResultAsync();
        });
    }

    private static ScraperService CreateScraperService() =>
        new(
            NullLogger<ScraperService>.Instance,
            new JsonLdRecipeExtractor(NullLogger<JsonLdRecipeExtractor>.Instance)
        );

    private static RecipeEmbeddingActivities CreateEmbeddingActivities(Exception? exception = null)
    {
        var embeddingService = Substitute.For<IRecipeEmbeddingService>();
        if (exception is null)
        {
            embeddingService
                .EnsureRecipeEmbeddingAsync(
                    default!,
                    default!,
                    default!,
                    default
                )
                .ReturnsForAnyArgs(Task.CompletedTask);
        }
        else
        {
            embeddingService
                .EnsureRecipeEmbeddingAsync(
                    default!,
                    default!,
                    default!,
                    default
                )
                .ReturnsForAnyArgs((Func<NSubstitute.Core.CallInfo, Task>)(_ => throw exception));
        }

        return new RecipeEmbeddingActivities(embeddingService, NullLogger<RecipeEmbeddingActivities>.Instance);
    }
}
