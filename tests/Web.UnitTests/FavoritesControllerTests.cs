using System.Security.Claims;
using App.Recipes;
using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using NSubstitute.Core;
using Web.Controllers;
using Web.Models;
using Xunit;

namespace Web.Tests;

public class FavoritesControllerTests
{
    private readonly IRecipeCollectionService _service = Substitute.For<IRecipeCollectionService>();

    [Fact]
    public async Task Toggle_Success_Returns200WithFavoriteState()
    {
        var userId = Guid.NewGuid();
        var recipeId = Guid.NewGuid();
        _service
            .ToggleFavoriteAsync(default, default, default)
            .ReturnsForAnyArgs(
                (Func<CallInfo, Task<ToggleRecipeResult>>)(
                    _ =>
                        Task.FromResult<ToggleRecipeResult>(
                            new ToggleRecipeResult.Success(true)
                        )
                )
            );
        var controller = CreateController(userId);

        var result = await controller.Toggle(recipeId, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);
        var response = ok.Value.Should().BeOfType<ToggleFavoriteResponse>().Subject;
        response.RecipeId.Should().Be(recipeId);
        response.IsFavorite.Should().BeTrue();
        await _service.Received(1).ToggleFavoriteAsync(userId, recipeId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Toggle_MissingRecipe_Returns404()
    {
        var userId = Guid.NewGuid();
        var recipeId = Guid.NewGuid();
        _service
            .ToggleFavoriteAsync(default, default, default)
            .ReturnsForAnyArgs(
                (Func<CallInfo, Task<ToggleRecipeResult>>)(
                    _ =>
                        Task.FromResult<ToggleRecipeResult>(
                            new ToggleRecipeResult.RecipeNotFound()
                        )
                )
            );
        var controller = CreateController(userId);

        var result = await controller.Toggle(recipeId, CancellationToken.None);

        var notFound = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFound.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task List_ReturnsFavoriteRecipeIds()
    {
        var userId = Guid.NewGuid();
        IReadOnlyList<Guid> recipeIds = [Guid.NewGuid(), Guid.NewGuid()];
        _service
            .ListFavoriteRecipeIdsAsync(default, default)
            .ReturnsForAnyArgs(
                (Func<CallInfo, Task<IReadOnlyList<Guid>>>)(_ => Task.FromResult(recipeIds))
            );
        var controller = CreateController(userId);

        var result = await controller.List(CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);
        var response = ok.Value.Should().BeOfType<ListFavoritesResponse>().Subject;
        response.RecipeIds.Should().Equal(recipeIds);
        await _service.Received(1).ListFavoriteRecipeIdsAsync(userId, Arg.Any<CancellationToken>());
    }

    private FavoritesController CreateController(Guid userId)
    {
        var controller = new FavoritesController(_service);
        var user = new ClaimsPrincipal(
            new ClaimsIdentity([new Claim("sub", userId.ToString())], "TestAuth")
        );
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user },
        };

        return controller;
    }
}
