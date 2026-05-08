using App.Notifications;
using Microsoft.AspNetCore.Mvc;
using Web.Models;

namespace Web.Controllers;

[ApiController]
[Route("api/v1/notification")]
public class NotificationsController(INotificationService notificationService) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Post(
        [FromBody] NotificationEnvelopeRequest envelope,
        CancellationToken cancellationToken
    )
    {
        var command = new SendNotificationCommand
        {
            Body = envelope.Body,
            TargetUrl = envelope.Metadata.TargetUrl,
            Headers = envelope.Metadata.Headers,
            Delay = envelope.Metadata.Delay,
        };

        await notificationService.SendAsync(command, cancellationToken);
        return Accepted();
    }
}
