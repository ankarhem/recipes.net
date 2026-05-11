namespace App.Crawler;

public sealed class StartCrawlJobCommand
{
    public required Uri TargetUrl { get; init; }
}
