using App.Identity;
using AwesomeAssertions;
using Domain.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using NSubstitute.Core;
using System.Security.Claims;
using Web.Controllers;
using Web.Models;

namespace Web.Tests;

public class TwoFactorControllerTests
{
    private readonly ITwoFactorService _service = Substitute.For<ITwoFactorService>();
    private readonly Guid _userId = Guid.NewGuid();

    private TwoFactorController CreateController()
    {
        var controller = new TwoFactorController(_service);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(
                    new ClaimsIdentity([new Claim("sub", _userId.ToString())], "TestAuth")
                ),
            },
        };
        return controller;
    }

    [Fact]
    public async Task Setup_Returns200WithSetupResponse()
    {
        _service
            .SetupAsync(default!)
            .ReturnsForAnyArgs(
                (Func<CallInfo, Task<TwoFactorResult>>)(
                    _ =>
                        Task.FromResult<TwoFactorResult>(
                            new TwoFactorResult.SetupPending("secret", "otpauth://...", "base64png")
                        )
                )
            );
        var controller = CreateController();

        var result = await controller.Setup(CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);
        var response = ok.Value.Should().BeOfType<TwoFactorSetupResponse>().Subject;
        response.Base32Secret.Should().Be("secret");
        response.OtpAuthUri.Should().Be("otpauth://...");
        response.QrCodePngBase64.Should().Be("base64png");
    }

    [Fact]
    public async Task Setup_AlreadyEnabled_Returns409()
    {
        _service
            .SetupAsync(default!)
            .ReturnsForAnyArgs(
                (Func<CallInfo, Task<TwoFactorResult>>)(
                    _ => Task.FromResult<TwoFactorResult>(new TwoFactorResult.AlreadyEnabled())
                )
            );
        var controller = CreateController();

        var result = await controller.Setup(CancellationToken.None);

        var conflict = result.Should().BeOfType<ConflictObjectResult>().Subject;
        conflict.StatusCode.Should().Be(409);
    }

    [Fact]
    public async Task Confirm_Returns200WithRecoveryCodes()
    {
        var codes = new List<string> { "code1", "code2" };
        _service
            .ConfirmAsync(default!, default!)
            .ReturnsForAnyArgs(
                (Func<CallInfo, Task<TwoFactorResult>>)(
                    _ => Task.FromResult<TwoFactorResult>(new TwoFactorResult.Enabled(codes))
                )
            );
        var controller = CreateController();

        var result = await controller.Confirm(
            new ConfirmTwoFactorRequest { Code = "123456" },
            CancellationToken.None
        );

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);
        var response = ok.Value.Should().BeOfType<TwoFactorEnabledResponse>().Subject;
        response.RecoveryCodes.Should().Equal("code1", "code2");
    }

    [Fact]
    public async Task Confirm_InvalidCode_Returns400()
    {
        _service
            .ConfirmAsync(default!, default!)
            .ReturnsForAnyArgs(
                (Func<CallInfo, Task<TwoFactorResult>>)(
                    _ => Task.FromResult<TwoFactorResult>(new TwoFactorResult.InvalidCode())
                )
            );
        var controller = CreateController();

        var result = await controller.Confirm(
            new ConfirmTwoFactorRequest { Code = "wrong" },
            CancellationToken.None
        );

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task Confirm_AlreadyEnabled_Returns409()
    {
        _service
            .ConfirmAsync(default!, default!)
            .ReturnsForAnyArgs(
                (Func<CallInfo, Task<TwoFactorResult>>)(
                    _ => Task.FromResult<TwoFactorResult>(new TwoFactorResult.AlreadyEnabled())
                )
            );
        var controller = CreateController();

        var result = await controller.Confirm(
            new ConfirmTwoFactorRequest { Code = "123456" },
            CancellationToken.None
        );

        var conflict = result.Should().BeOfType<ConflictObjectResult>().Subject;
        conflict.StatusCode.Should().Be(409);
    }

    [Fact]
    public async Task Disable_Returns200()
    {
        _service
            .DisableAsync(default!, default!)
            .ReturnsForAnyArgs(
                (Func<CallInfo, Task<TwoFactorResult>>)(
                    _ => Task.FromResult<TwoFactorResult>(new TwoFactorResult.Disabled())
                )
            );
        var controller = CreateController();

        var result = await controller.Disable(
            new DisableTwoFactorRequest { Code = "123456" },
            CancellationToken.None
        );

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task Disable_InvalidCode_Returns400()
    {
        _service
            .DisableAsync(default!, default!)
            .ReturnsForAnyArgs(
                (Func<CallInfo, Task<TwoFactorResult>>)(
                    _ => Task.FromResult<TwoFactorResult>(new TwoFactorResult.InvalidCode())
                )
            );
        var controller = CreateController();

        var result = await controller.Disable(
            new DisableTwoFactorRequest { Code = "wrong" },
            CancellationToken.None
        );

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task Disable_NotEnabled_Returns400()
    {
        _service
            .DisableAsync(default!, default!)
            .ReturnsForAnyArgs(
                (Func<CallInfo, Task<TwoFactorResult>>)(
                    _ => Task.FromResult<TwoFactorResult>(new TwoFactorResult.NotEnabled())
                )
            );
        var controller = CreateController();

        var result = await controller.Disable(
            new DisableTwoFactorRequest { Code = "123456" },
            CancellationToken.None
        );

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task RegenerateRecoveryCodes_Returns200WithNewCodes()
    {
        var codes = new List<string> { "new1", "new2" };
        _service
            .RegenerateRecoveryCodesAsync(default!, default!)
            .ReturnsForAnyArgs(
                (Func<CallInfo, Task<TwoFactorResult>>)(
                    _ =>
                        Task.FromResult<TwoFactorResult>(
                            new TwoFactorResult.CodesRegenerated(codes)
                        )
                )
            );
        var controller = CreateController();

        var result = await controller.RegenerateRecoveryCodes(
            new RegenerateRecoveryCodesRequest { Code = "123456" },
            CancellationToken.None
        );

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);
        var response = ok.Value.Should().BeOfType<RecoveryCodesResponse>().Subject;
        response.RecoveryCodes.Should().Equal("new1", "new2");
    }

    [Fact]
    public async Task RegenerateRecoveryCodes_InvalidCode_Returns400()
    {
        _service
            .RegenerateRecoveryCodesAsync(default!, default!)
            .ReturnsForAnyArgs(
                (Func<CallInfo, Task<TwoFactorResult>>)(
                    _ => Task.FromResult<TwoFactorResult>(new TwoFactorResult.InvalidCode())
                )
            );
        var controller = CreateController();

        var result = await controller.RegenerateRecoveryCodes(
            new RegenerateRecoveryCodesRequest { Code = "wrong" },
            CancellationToken.None
        );

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task RegenerateRecoveryCodes_NotEnabled_Returns400()
    {
        _service
            .RegenerateRecoveryCodesAsync(default!, default!)
            .ReturnsForAnyArgs(
                (Func<CallInfo, Task<TwoFactorResult>>)(
                    _ => Task.FromResult<TwoFactorResult>(new TwoFactorResult.NotEnabled())
                )
            );
        var controller = CreateController();

        var result = await controller.RegenerateRecoveryCodes(
            new RegenerateRecoveryCodesRequest { Code = "123456" },
            CancellationToken.None
        );

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task Setup_WithoutAuthenticatedUser_Returns401()
    {
        var controller = new TwoFactorController(_service);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal() },
        };

        var result = await controller.Setup(CancellationToken.None);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task Confirm_WithUserNotFound_Returns401()
    {
        _service
            .ConfirmAsync(default!, default!)
            .ReturnsForAnyArgs(
                (Func<CallInfo, Task<TwoFactorResult>>)(
                    _ => Task.FromResult<TwoFactorResult>(new TwoFactorResult.UserNotFound())
                )
            );
        var controller = CreateController();

        var result = await controller.Confirm(
            new ConfirmTwoFactorRequest { Code = "123456" },
            CancellationToken.None
        );

        result.Should().BeOfType<UnauthorizedResult>();
    }
}