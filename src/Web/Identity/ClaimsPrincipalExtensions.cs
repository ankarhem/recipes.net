using System.Security.Claims;

namespace Web.Identity;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var sub = principal.FindFirst("sub")?.Value;

        if (!Guid.TryParse(sub, out var userId))
        {
            throw new InvalidOperationException("User has no valid 'sub' claim.");
        }

        return userId;
    }
}
