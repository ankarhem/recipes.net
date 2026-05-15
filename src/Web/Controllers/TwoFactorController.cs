using App.Identity;
using Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Web.Identity;
using Web.Models;

namespace Web.Controllers;

[ApiController]
[Route("api/v1/auth/2fa")]
[Authorize]
[EnableRateLimiting("twofa")]
[Tags("Auth")]
public sealed class TwoFactorController(ITwoFactorService twoFactor) : ControllerBase
{
    /// <summary>
    /// Start two-factor authentication setup for the authenticated user.
    /// </summary>
    /// <response code="200">Setup secret and QR code generated.</response>
    /// <response code="401">Authentication is required.</response>
    /// <response code="409">Two-factor authentication is already enabled.</response>
    [HttpPost("setup")]
    [ProducesResponseType<TwoFactorSetupResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Setup(CancellationToken cancellationToken)
    {
        var userId = GetAuthenticatedUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await twoFactor.SetupAsync(userId.Value);
        return MapResult(result);
    }

    /// <summary>
    /// Confirm two-factor authentication setup with a valid TOTP code.
    /// </summary>
    /// <response code="200">Two-factor authentication enabled.</response>
    /// <response code="400">Invalid two-factor code or setup has not started.</response>
    /// <response code="401">Authentication is required.</response>
    /// <response code="409">Two-factor authentication is already enabled.</response>
    [HttpPost("confirm")]
    [ProducesResponseType<TwoFactorEnabledResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Confirm(
        ConfirmTwoFactorRequest request,
        CancellationToken cancellationToken
    )
    {
        var userId = GetAuthenticatedUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await twoFactor.ConfirmAsync(userId.Value, request.Code);
        return MapResult(result);
    }

    /// <summary>
    /// Disable two-factor authentication for the authenticated user.
    /// </summary>
    /// <response code="200">Two-factor authentication disabled.</response>
    /// <response code="400">Invalid two-factor code or two-factor authentication is not enabled.</response>
    /// <response code="401">Authentication is required.</response>
    [HttpPost("disable")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Disable(
        DisableTwoFactorRequest request,
        CancellationToken cancellationToken
    )
    {
        var userId = GetAuthenticatedUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await twoFactor.DisableAsync(userId.Value, request.Code);
        return MapResult(result);
    }

    /// <summary>
    /// Regenerate recovery codes for the authenticated user.
    /// </summary>
    /// <response code="200">Recovery codes regenerated.</response>
    /// <response code="400">Invalid two-factor code or two-factor authentication is not enabled.</response>
    /// <response code="401">Authentication is required.</response>
    [HttpPost("recovery-codes/regenerate")]
    [ProducesResponseType<RecoveryCodesResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RegenerateRecoveryCodes(
        RegenerateRecoveryCodesRequest request,
        CancellationToken cancellationToken
    )
    {
        var userId = GetAuthenticatedUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await twoFactor.RegenerateRecoveryCodesAsync(userId.Value, request.Code);
        return MapResult(result);
    }

    private UserId? GetAuthenticatedUserId()
    {
        try
        {
            return new UserId(User.GetUserId());
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private IActionResult MapResult(TwoFactorResult result) =>
        result switch
        {
            TwoFactorResult.SetupPending setup => Ok(
                new TwoFactorSetupResponse(
                    setup.Base32Secret,
                    setup.OtpAuthUri,
                    setup.QrCodePngBase64
                )
            ),
            TwoFactorResult.Enabled enabled => Ok(
                new TwoFactorEnabledResponse(enabled.RecoveryCodes)
            ),
            TwoFactorResult.CodesRegenerated regenerated => Ok(
                new RecoveryCodesResponse(regenerated.RecoveryCodes)
            ),
            TwoFactorResult.Disabled => Ok(new { message = "Two-factor authentication disabled." }),
            TwoFactorResult.InvalidCode => BadRequest(new { error = "Invalid two-factor code." }),
            TwoFactorResult.AlreadyEnabled => Conflict(
                new { error = "Two-factor authentication is already enabled." }
            ),
            TwoFactorResult.NotEnabled => BadRequest(
                new { error = "Two-factor authentication is not enabled." }
            ),
            TwoFactorResult.UserNotFound => Unauthorized(),
            _ => StatusCode(StatusCodes.Status500InternalServerError),
        };
}
