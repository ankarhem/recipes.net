using App.Notifications;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Web.Models;

namespace Web.Controllers;

[ApiController]
[Route("api/v1/notification")]
public class NotificationsController(
    INotificationService notificationService,
    ILogger<NotificationsController> logger
) : ControllerBase
{
    public NotificationsController(INotificationService notificationService)
        : this(notificationService, NullLogger<NotificationsController>.Instance) { }

    [HttpPost]
    public async Task<IActionResult> Post(
        [FromBody] NotificationEnvelopeRequest envelope,
        CancellationToken cancellationToken
    )
    {
        logger.LogInformation(
            "Notification request received for {TargetUrl}",
            envelope.Metadata.TargetUrl
        );

        var command = new SendNotificationCommand
        {
            Body = envelope.Body,
            TargetUrl = envelope.Metadata.TargetUrl,
            Headers = envelope.Metadata.Headers,
            Delay = envelope.Metadata.Delay,
            NotificationId = envelope.NotificationId ?? Guid.NewGuid().ToString(),
        };

        var notificationId = await notificationService.SendAsync(command, cancellationToken);
        logger.LogInformation(
            "Notification accepted with ID {NotificationId}",
            command.NotificationId
        );

        return Accepted(new { notificationId });
    }
}
