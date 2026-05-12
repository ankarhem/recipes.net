using System.ComponentModel.DataAnnotations;

namespace Web.Models;

public sealed class StartCrawlRequest
{
    [Required]
    public Uri? TargetUrl { get; init; }
}
