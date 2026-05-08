using System.Reflection;
using Microsoft.AspNetCore.Mvc;

namespace Web.Controllers;

[ApiController]
[Route("/healthcheck")]
public class HealthCheckController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        var informationalVersion = Assembly
            .GetEntryAssembly()!
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
            .InformationalVersion;

        var plusIndex = informationalVersion.IndexOf('+');
        var commit = plusIndex >= 0 ? informationalVersion[(plusIndex + 1)..] : "unknown";

        return Ok(new { commit });
    }
}
