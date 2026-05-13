using App.Embedding;
using AwesomeAssertions;
using Xunit;
using DomainRecipe = Domain.Recipe.Recipe;
using DomainRecipeIngredient = Domain.Recipe.RecipeIngredient;
using DomainRecipeInstruction = Domain.Recipe.RecipeInstruction;

namespace App.UnitTests;

public class RecipeEmbeddingTextBuilderTests
{
    private readonly RecipeEmbeddingTextBuilder _builder = new();

    [Fact]
    public void Build_WithFullRecipe_ProducesCanonicalText()
    {
        var recipe = new DomainRecipe
        {
            Name = "Tomato Soup",
            Description = "A simple tomato soup",
            ImageUrls = Array.Empty<string>(),
            Ingredients = new[]
            {
                new DomainRecipeIngredient { Text = "4 tomatoes" },
                new DomainRecipeIngredient { Text = "1 cup stock" },
            },
            Instructions = new[]
            {
                new DomainRecipeInstruction { Position = 1, Text = "Chop tomatoes" },
                new DomainRecipeInstruction { Position = 2, Text = "Simmer soup" },
            },
        };

        var result = _builder.Build(recipe);

        result
            .Should()
            .Be(
                JoinLines(
                    "Name: Tomato Soup",
                    "Description: A simple tomato soup",
                    "Ingredients:",
                    "- 4 tomatoes",
                    "- 1 cup stock",
                    "Instructions:",
                    "1. Chop tomatoes",
                    "2. Simmer soup"
                )
            );
    }

    [Fact]
    public void Build_WithNullName_OmitsNameLine()
    {
        var recipe = MinimalRecipe() with { Description = "A simple soup" };

        var result = _builder.Build(recipe);

        result.Should().Be("Description: A simple soup");
    }

    [Fact]
    public void Build_WithNullDescription_OmitsDescriptionLine()
    {
        var recipe = MinimalRecipe() with { Name = "Tomato Soup" };

        var result = _builder.Build(recipe);

        result.Should().Be("Name: Tomato Soup");
    }

    [Fact]
    public void Build_WithEmptyIngredients_OmitsIngredientsSection()
    {
        var recipe = MinimalRecipe() with
        {
            Name = "Tomato Soup",
            Description = "A simple tomato soup",
            Instructions = new[]
            {
                new DomainRecipeInstruction { Position = 1, Text = "Simmer soup" },
            },
        };

        var result = _builder.Build(recipe);

        result
            .Should()
            .Be(
                JoinLines(
                    "Name: Tomato Soup",
                    "Description: A simple tomato soup",
                    "Instructions:",
                    "1. Simmer soup"
                )
            );
    }

    [Fact]
    public void Build_WithEmptyInstructions_OmitsInstructionsSection()
    {
        var recipe = MinimalRecipe() with
        {
            Name = "Tomato Soup",
            Description = "A simple tomato soup",
            Ingredients = new[] { new DomainRecipeIngredient { Text = "4 tomatoes" } },
        };

        var result = _builder.Build(recipe);

        result
            .Should()
            .Be(
                JoinLines(
                    "Name: Tomato Soup",
                    "Description: A simple tomato soup",
                    "Ingredients:",
                    "- 4 tomatoes"
                )
            );
    }

    [Fact]
    public void Build_WithMinimalRecipe_ProducesExpectedFormat()
    {
        var recipe = MinimalRecipe();

        var result = _builder.Build(recipe);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Build_IsDeterministic_SameInputSameOutput()
    {
        var recipe = new DomainRecipe
        {
            Name = "Tomato Soup",
            Description = "A simple tomato soup",
            ImageUrls = Array.Empty<string>(),
            Ingredients = new[]
            {
                new DomainRecipeIngredient { Text = "4 tomatoes" },
                new DomainRecipeIngredient { Text = "1 cup stock" },
            },
            Instructions = new[]
            {
                new DomainRecipeInstruction { Position = 1, Text = "Chop tomatoes" },
                new DomainRecipeInstruction { Position = 2, Text = "Simmer soup" },
            },
        };

        var first = _builder.Build(recipe);
        var second = _builder.Build(recipe);

        second.Should().Be(first);
    }

    [Fact]
    public void ComputeInputHash_ProducesStableLowercaseSha256()
    {
        var result = RecipeEmbeddingTextBuilder.ComputeInputHash("hello");

        result.Should().Be("2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824");
    }

    private static DomainRecipe MinimalRecipe() =>
        new()
        {
            ImageUrls = Array.Empty<string>(),
            Ingredients = Array.Empty<DomainRecipeIngredient>(),
            Instructions = Array.Empty<DomainRecipeInstruction>(),
        };

    private static string JoinLines(params string[] lines) => string.Join("\n", lines);
}
