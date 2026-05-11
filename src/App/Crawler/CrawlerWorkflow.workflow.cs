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

        return;
    }

    private async Task HandlePageAsync(Uri url)
    {
        if (_visitedUrls.Contains(url))
        {
            return;
        }

        var pageContent = await Workflow.ExecuteActivityAsync(
            (CrawlerActivities a) => a.GetRecipePage(url),
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

        // Extract links
        // Extract Recipe
        // Save recipe
        // Enqueue non-visited links

        _visitedUrls.Add(url);
    }
}
