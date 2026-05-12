using Microsoft.Extensions.Logging;
using Temporalio.Workflows;

namespace App.Crawler;

[Workflow]
public class CrawlerWorkflow
{
    private readonly HashSet<Uri> _visitedUrls = new();
    private readonly Queue<Uri> _urlQueue = new();

    [WorkflowRun]
    public async Task RunAsync(StartCrawlJobCommand command)
    {
        _urlQueue.Enqueue(command.TargetUrl);

        while (_urlQueue.Count > 0)
        {
            var url = _urlQueue.Dequeue();
            await Workflow.DelayAsync(TimeSpan.FromMilliseconds(new Random().Next(300, 1000)));
            await HandlePageAsync(url);
        }
    }

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

        var links = await Workflow.ExecuteLocalActivityAsync(
            (CrawlerActivities a) => a.ExtractLinksAsync(pageContent, url),
            new() { StartToCloseTimeout = TimeSpan.FromSeconds(5) }
        );

        var extracted = await Workflow.ExecuteLocalActivityAsync(
            (CrawlerActivities a) => a.ExtractRecipe(pageContent),
            new() { StartToCloseTimeout = TimeSpan.FromSeconds(5) }
        );

        if (extracted is not null)
        {
            var recipe = RecipeFactory.FromSchema(extracted.SchemaRecipe);

            await Workflow.ExecuteActivityAsync(
                (CrawlerActivities a) =>
                    a.SaveRecipeAsync(recipe, url.ToString(), extracted.RawJsonLd),
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

        foreach (var link in links)
        {
            if (!_visitedUrls.Contains(link))
            {
                _urlQueue.Enqueue(link);
            }
        }
    }
}
