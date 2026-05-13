using Microsoft.AspNetCore.Diagnostics;

namespace Web.ExceptionHandling;

public sealed class LoggingExceptionHandler(ILogger<LoggingExceptionHandler> logger) : IExceptionHandler
{
    public ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        logger.LogError(
            exception,
            "Unhandled exception: {ExceptionType} at {Path}",
            exception.GetType().Name,
            httpContext.Request.Path);

        return ValueTask.FromResult(false);
    }
}