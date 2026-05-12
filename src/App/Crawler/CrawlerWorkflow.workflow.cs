using App.Recipe;
using Microsoft.Extensions.Logging;
using Temporalio.Workflows;

namespace App.Crawler;

[Workflow]
public class CrawlerWorkflow
{
    private readonly HashSet<Uri> _visitedUrls = new();
    private readonly Queue<Uri> _urlQueue = new();
    private bool _isPaused;

    [WorkflowRun]
    public async Task RunAsync(StartCrawlJobCommand command)
    {
        _urlQueue.Enqueue(command.TargetUrl);

        while (_urlQueue.Count > 0)
        {
            if (_isPaused)
            {
                await Workflow.WaitConditionAsync(() => !_isPaused);
            }

            var url = _urlQueue.Dequeue();
            await Workflow.DelayAsync(TimeSpan.FromMilliseconds(Workflow.Random.Next(300, 1000)));
            await HandlePageAsync(url);
        }
    }

    [WorkflowSignal]
    public async Task PauseAsync() => _isPaused = true;

    [WorkflowSignal]
    public async Task ResumeAsync() => _isPaused = false;

    [WorkflowQuery]
    public bool IsPaused => _isPaused;

    [WorkflowQuery]
    public CrawlStatus GetStatus() =>
        new("running", _visitedUrls.Count, _urlQueue.Count, _isPaused);

    private async Task HandlePageAsync(Uri url)
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

        if (page.Recipe is not null)
        {
            await Workflow.ExecuteActivityAsync(
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
        }

        foreach (var link in page.Links)
        {
            if (!_visitedUrls.Contains(link))
            {
                _urlQueue.Enqueue(link);
            }
        }
    }
}
