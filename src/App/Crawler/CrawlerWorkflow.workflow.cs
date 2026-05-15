using App.Recipes;
using Microsoft.Extensions.Logging;
using Temporalio.Exceptions;
using Temporalio.Workflows;

namespace App.Crawler;

[Workflow]
public class CrawlerWorkflow
{
    private const int ContinueAsNewThreshold = 200;

    private readonly HashSet<Uri> _visitedUrls = new();
    private readonly HashSet<Uri> _queuedUrls = new();
    private readonly Queue<Uri> _urlQueue = new();
    private bool _isPaused;

    [WorkflowRun]
    public async Task RunAsync(StartCrawlJobCommand command)
    {
        foreach (var url in command.Visited)
        {
            _visitedUrls.Add(url);
        }

        foreach (var url in command.Queue)
        {
            if (_visitedUrls.Contains(url) || !_queuedUrls.Add(url))
            {
                continue;
            }
            _urlQueue.Enqueue(url);
        }

        if (_visitedUrls.Count == 0 && _urlQueue.Count == 0)
        {
            _urlQueue.Enqueue(command.TargetUrl);
            _queuedUrls.Add(command.TargetUrl);
        }

        var visitedAtStart = _visitedUrls.Count;

        while (_urlQueue.Count > 0)
        {
            if (_isPaused)
            {
                await Workflow.WaitConditionAsync(() => !_isPaused);
            }

            if (_visitedUrls.Count >= command.MaxPages)
            {
                Workflow.Logger.LogInformation(
                    "Reached MaxPages cap of {MaxPages} for {TargetUrl}",
                    command.MaxPages,
                    command.TargetUrl
                );
                return;
            }

            var pagesThisRun = _visitedUrls.Count - visitedAtStart;
            if (pagesThisRun >= ContinueAsNewThreshold)
            {
                throw Workflow.CreateContinueAsNewException<CrawlerWorkflow>(wf =>
                    wf.RunAsync(
                        new StartCrawlJobCommand
                        {
                            TargetUrl = command.TargetUrl,
                            MaxPages = command.MaxPages,
                            EmbeddingModel = command.EmbeddingModel,
                            Queue = _urlQueue.ToList(),
                            Visited = _visitedUrls.ToList(),
                        }
                    )
                );
            }

            var url = _urlQueue.Dequeue();
            _queuedUrls.Remove(url);

            await Workflow.DelayAsync(TimeSpan.FromMilliseconds(Workflow.Random.Next(300, 1000)));

            try
            {
                await HandlePageAsync(url, command);
            }
            catch (ActivityFailureException ex)
            {
                Workflow.Logger.LogWarning(
                    ex,
                    "Activity failed for {Url}, continuing crawl",
                    url
                );
            }
        }
    }

    [WorkflowSignal]
    public async Task PauseAsync() => _isPaused = true;

    [WorkflowSignal]
    public async Task ResumeAsync() => _isPaused = false;

    [WorkflowQuery]
    public CrawlState GetState() =>
        new()
        {
            UrlsCrawled = _visitedUrls.Count,
            UrlsQueued = _urlQueue.Count,
            IsPaused = _isPaused,
        };

    private async Task HandlePageAsync(Uri url, StartCrawlJobCommand command)
    {
        if (_visitedUrls.Contains(url))
        {
            return;
        }

        _visitedUrls.Add(url);

        var pageContent = await Workflow.ExecuteActivityAsync(
            (CrawlerActivities a) => a.FetchPageAsync(url),
            new()
            {
                StartToCloseTimeout = TimeSpan.FromSeconds(10),
                RetryPolicy = new()
                {
                    InitialInterval = TimeSpan.FromSeconds(1),
                    BackoffCoefficient = 2.0F,
                    MaximumInterval = TimeSpan.FromSeconds(30),
                    MaximumAttempts = 5,
                },
            }
        );

        if (pageContent is null)
        {
            Workflow.Logger.LogWarning("Fetch returned null for {Url}", url);
            return;
        }

        var page = await Workflow.ExecuteLocalActivityAsync(
            (CrawlerActivities a) => a.ExtractPageAsync(pageContent, url),
            new() { StartToCloseTimeout = TimeSpan.FromSeconds(5) }
        );

        foreach (var link in page.Links)
        {
            if (_visitedUrls.Contains(link) || !_queuedUrls.Add(link))
            {
                continue;
            }
            _urlQueue.Enqueue(link);
        }

        if (page.Recipe is not null)
        {
            var recipeId = await Workflow.ExecuteActivityAsync(
                (CrawlerActivities a) =>
                    a.SaveRecipeAsync(page.Recipe, url.ToString(), page.RawJsonLd!),
                new()
                {
                    StartToCloseTimeout = TimeSpan.FromSeconds(10),
                    RetryPolicy = new()
                    {
                        InitialInterval = TimeSpan.FromSeconds(1),
                        BackoffCoefficient = 2.0F,
                        MaximumInterval = TimeSpan.FromSeconds(30),
                        MaximumAttempts = 3,
                    },
                }
            );

            await Workflow.ExecuteActivityAsync(
                (RecipeEmbeddingActivities a) =>
                    a.EnsureRecipeEmbeddingAsync(recipeId, page.Recipe, command.EmbeddingModel),
                new()
                {
                    StartToCloseTimeout = TimeSpan.FromSeconds(30),
                    RetryPolicy = new()
                    {
                        InitialInterval = TimeSpan.FromSeconds(2),
                        BackoffCoefficient = 2.0F,
                        MaximumInterval = TimeSpan.FromSeconds(60),
                        MaximumAttempts = 2,
                    },
                }
            );
        }
    }
}
