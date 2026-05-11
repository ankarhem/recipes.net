using Schema.NET;
using Temporalio.Workflows;

namespace App.Crawler;

[Workflow]
public class CrawlerWorkflow
{
    private readonly HashSet<Uri> _visitedUrls = new();
    private readonly Queue<Uri> _urlQueue = new();
    private readonly List<Recipe> _foundRecipes = new();

    [WorkflowRun]
    public async Task<IReadOnlyList<Recipe>> RunAsync(StartCrawlJobCommand command)
    {
        _urlQueue.Enqueue(command.TargetUrl);

        while (_urlQueue.Count > 0)
        {
            var url = _urlQueue.Dequeue();
            await Workflow.DelayAsync(TimeSpan.FromMilliseconds(new Random().Next(300, 1000)));
            await HandlePageAsync(url);
        }

        return _foundRecipes;
    }

    private async Task HandlePageAsync(Uri url)
    {
        if (_visitedUrls.Contains(url))
        {
            return;
        }

        _visitedUrls.Add(url);

        var crawlCommand = new StartCrawlJobCommand { TargetUrl = url };

        var pageContent = await Workflow.ExecuteActivityAsync(
            (CrawlerActivities a) => a.CrawlAsync(crawlCommand),
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
            return;

        var links = await Workflow.ExecuteLocalActivityAsync(
            (CrawlerActivities a) => a.ExtractLinksAsync(pageContent, url),
            new() { StartToCloseTimeout = TimeSpan.FromSeconds(5) }
        );

        var recipeJson = await Workflow.ExecuteLocalActivityAsync(
            (CrawlerActivities a) => a.ExtractRecipeJsonLd(pageContent),
            new() { StartToCloseTimeout = TimeSpan.FromSeconds(5) }
        );

        if (recipeJson is not null)
        {
            var recipe = SchemaSerializer.DeserializeObject<Recipe>(recipeJson);
            if (recipe is not null)
                _foundRecipes.Add(recipe);
        }

        // Save recipe

        foreach (var link in links)
        {
            if (!_visitedUrls.Contains(link))
            {
                _urlQueue.Enqueue(link);
            }
        }
    }
}
