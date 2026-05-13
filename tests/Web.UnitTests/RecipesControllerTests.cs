using App.Recipe;
using AwesomeAssertions;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Web.Controllers;
using Web.Models;
using Xunit;

namespace Web.Tests;

public class RecipesControllerTests
{
    private readonly IRecipeService _service = Substitute.For<IRecipeService>();

    private static readonly Domain.Recipe.Recipe TestRecipe = new()
    {
        Name = "Test Recipe",
        Description = "A test",
        ImageUrls = ["https://example.com/image.jpg"],
        Ingredients = [new Domain.Recipe.RecipeIngredient { Text = "1 cup flour" }],
        Instructions =
        [
            new Domain.Recipe.RecipeInstruction { Position = 1, Text = "Mix dry ingredients" },
            new Domain.Recipe.RecipeInstruction { Position = 2, Text = "Add wet ingredients" },
        ],
    };

    [Fact]
    public async Task Get_ExistingId_Returns200WithRecipe()
    {
        var id = Guid.NewGuid();
        _service
            .GetRecipeAsync(id, Arg.Any<CancellationToken>())
            .Returns(
                (Func<NSubstitute.Core.CallInfo, Task<Domain.Recipe.Recipe?>>)(
                    _ => Task.FromResult<Domain.Recipe.Recipe?>(TestRecipe)
                )
            );
        var controller = new RecipesController(_service);

        var result = await controller.Get(id, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);
        var response = ok.Value.Should().BeOfType<GetRecipeResponse>().Subject;
        response.Id.Should().Be(id);
        response.Name.Should().Be("Test Recipe");
        response.Description.Should().Be("A test");
        response.ImageUrls.Should().Equal("https://example.com/image.jpg");
        response.Ingredients.Should().HaveCount(1);
        response.Ingredients[0].Text.Should().Be("1 cup flour");
        response.Instructions.Should().HaveCount(2);
        response.Instructions[0].Position.Should().Be(1);
        response.Instructions[0].Text.Should().Be("Mix dry ingredients");
        response.Instructions[1].Position.Should().Be(2);
        response.Instructions[1].Text.Should().Be("Add wet ingredients");
    }

    [Fact]
    public async Task Get_UnknownId_Returns404()
    {
        var id = Guid.NewGuid();
        _service
            .GetRecipeAsync(id, Arg.Any<CancellationToken>())
            .Returns(
                (Func<NSubstitute.Core.CallInfo, Task<Domain.Recipe.Recipe?>>)(
                    _ => Task.FromResult<Domain.Recipe.Recipe?>(null)
                )
            );
        var controller = new RecipesController(_service);

        var result = await controller.Get(id, CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Get_NullName_ReturnsUntitled()
    {
        var id = Guid.NewGuid();
        var recipe = TestRecipe with { Name = null };
        _service
            .GetRecipeAsync(id, Arg.Any<CancellationToken>())
            .Returns(
                (Func<NSubstitute.Core.CallInfo, Task<Domain.Recipe.Recipe?>>)(
                    _ => Task.FromResult<Domain.Recipe.Recipe?>(recipe)
                )
            );
        var controller = new RecipesController(_service);

        var result = await controller.Get(id, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<GetRecipeResponse>().Subject;
        response.Name.Should().Be("Untitled");
    }

    [Fact]
    public async Task Get_ForwardsCancellationToken()
    {
        var id = Guid.NewGuid();
        _service
            .GetRecipeAsync(id, Arg.Any<CancellationToken>())
            .Returns(
                (Func<NSubstitute.Core.CallInfo, Task<Domain.Recipe.Recipe?>>)(
                    _ => Task.FromResult<Domain.Recipe.Recipe?>(TestRecipe)
                )
            );
        var controller = new RecipesController(_service);
        using var cts = new CancellationTokenSource();

        await controller.Get(id, cts.Token);

        await _service.Received(1).GetRecipeAsync(id, cts.Token);
    }
}