using App.Recipes;
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

    private static readonly Domain.Recipes.Recipe TestRecipe = new()
    {
        Id = Guid.NewGuid(),
        Name = "Test Recipe",
        Description = "A test",
        ImageUrls = ["https://example.com/image.jpg"],
        Ingredients = [new Domain.Recipes.RecipeIngredient { Text = "1 cup flour" }],
        Instructions =
        [
            new Domain.Recipes.RecipeInstruction { Position = 1, Text = "Mix dry ingredients" },
            new Domain.Recipes.RecipeInstruction { Position = 2, Text = "Add wet ingredients" },
        ],
    };

    [Fact]
    public async Task Get_ExistingId_Returns200WithRecipe()
    {
        _service
            .GetRecipeAsync(TestRecipe.Id, Arg.Any<CancellationToken>())
            .Returns(
                (Func<NSubstitute.Core.CallInfo, Task<Domain.Recipes.Recipe?>>)(
                    _ => Task.FromResult<Domain.Recipes.Recipe?>(TestRecipe)
                )
            );
        var controller = new RecipesController(_service);

        var result = await controller.Get(TestRecipe.Id, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);
        var response = ok.Value.Should().BeOfType<GetRecipeResponse>().Subject;
        response.Id.Should().Be(TestRecipe.Id);
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
                (Func<NSubstitute.Core.CallInfo, Task<Domain.Recipes.Recipe?>>)(
                    _ => Task.FromResult<Domain.Recipes.Recipe?>(null)
                )
            );
        var controller = new RecipesController(_service);

        var result = await controller.Get(id, CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Get_NullName_ReturnsUntitled()
    {
        var recipe = TestRecipe with { Name = null };
        _service
            .GetRecipeAsync(recipe.Id, Arg.Any<CancellationToken>())
            .Returns(
                (Func<NSubstitute.Core.CallInfo, Task<Domain.Recipes.Recipe?>>)(
                    _ => Task.FromResult<Domain.Recipes.Recipe?>(recipe)
                )
            );
        var controller = new RecipesController(_service);

        var result = await controller.Get(recipe.Id, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<GetRecipeResponse>().Subject;
        response.Name.Should().Be("Untitled");
    }

    [Fact]
    public async Task Get_ForwardsCancellationToken()
    {
        _service
            .GetRecipeAsync(TestRecipe.Id, Arg.Any<CancellationToken>())
            .Returns(
                (Func<NSubstitute.Core.CallInfo, Task<Domain.Recipes.Recipe?>>)(
                    _ => Task.FromResult<Domain.Recipes.Recipe?>(TestRecipe)
                )
            );
        var controller = new RecipesController(_service);
        using var cts = new CancellationTokenSource();

        await controller.Get(TestRecipe.Id, cts.Token);

        await _service.Received(1).GetRecipeAsync(TestRecipe.Id, cts.Token);
    }

    [Fact]
    public async Task Search_ValidQuery_Returns200WithResults()
    {
        var results = new List<Domain.Recipes.Recipe> { TestRecipe };
        _service
            .SearchRecipesAsync("pasta", Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(
                (Func<NSubstitute.Core.CallInfo, Task<IReadOnlyList<Domain.Recipes.Recipe>>>)(
                    _ => Task.FromResult<IReadOnlyList<Domain.Recipes.Recipe>>(results)
                )
            );
        var controller = new RecipesController(_service);

        var result = await controller.Search("pasta", 10, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);
        var response = ok.Value.Should().BeOfType<SearchRecipesResponse>().Subject;
        response.Query.Should().Be("pasta");
        response.Results.Should().HaveCount(1);
        response.Results[0].Id.Should().Be(TestRecipe.Id);
        response.Results[0].Name.Should().Be("Test Recipe");
    }

    [Fact]
    public async Task Search_EmptyQuery_Returns400()
    {
        var controller = new RecipesController(_service);

        var result = await controller.Search("", 10, CancellationToken.None);

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task Search_WhitespaceQuery_Returns400()
    {
        var controller = new RecipesController(_service);

        var result = await controller.Search("   ", 10, CancellationToken.None);

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task Search_ForwardsCancellationToken()
    {
        _service
            .SearchRecipesAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(
                (Func<NSubstitute.Core.CallInfo, Task<IReadOnlyList<Domain.Recipes.Recipe>>>)(
                    _ => Task.FromResult<IReadOnlyList<Domain.Recipes.Recipe>>([])
                )
            );
        var controller = new RecipesController(_service);
        using var cts = new CancellationTokenSource();

        await controller.Search("pasta", 10, cts.Token);

        await _service.Received(1).SearchRecipesAsync("pasta", 10, cts.Token);
    }

    [Fact]
    public async Task Search_ClampsLimitToRange()
    {
        _service
            .SearchRecipesAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(
                (Func<NSubstitute.Core.CallInfo, Task<IReadOnlyList<Domain.Recipes.Recipe>>>)(
                    _ => Task.FromResult<IReadOnlyList<Domain.Recipes.Recipe>>([])
                )
            );
        var controller = new RecipesController(_service);

        await controller.Search("pasta", 0, CancellationToken.None);
        await _service.Received(1).SearchRecipesAsync("pasta", 1, Arg.Any<CancellationToken>());

        await controller.Search("pasta", 200, CancellationToken.None);
        await _service.Received(1).SearchRecipesAsync("pasta", 100, Arg.Any<CancellationToken>());
    }
}