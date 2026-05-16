using AwesomeAssertions;
using Domain.Recipes;
using Xunit;

namespace App.UnitTests;

public class RecipeEmbeddingTextBuilderTests
{
    private readonly RecipeEmbeddingTextBuilder _builder = new();

    [Fact]
    public void Build_WithFullRecipe_ProducesCanonicalText()
    {
        var recipe = Recipe.FromImport(
            "Tomato Soup",
            "A simple tomato soup",
            [],
            ["4 tomatoes", "1 cup stock"],
            ["Chop tomatoes", "Simmer soup"],
            null,
            null,
            [],
            null,
            null,
            null,
            null
        );

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
        var recipe = MinimalRecipe(description: "A simple soup");

        var result = _builder.Build(recipe);

        result.Should().Be("Description: A simple soup");
    }

    [Fact]
    public void Build_WithNullDescription_OmitsDescriptionLine()
    {
        var recipe = MinimalRecipe(name: "Tomato Soup");

        var result = _builder.Build(recipe);

        result.Should().Be("Name: Tomato Soup");
    }

    [Fact]
    public void Build_WithEmptyIngredients_OmitsIngredientsSection()
    {
        var recipe = MinimalRecipe(
            name: "Tomato Soup",
            description: "A simple tomato soup",
            instructionTexts: ["Simmer soup"]
        );

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
        var recipe = MinimalRecipe(
            name: "Tomato Soup",
            description: "A simple tomato soup",
            ingredientTexts: ["4 tomatoes"]
        );

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
        var recipe = Recipe.FromImport(
            "Tomato Soup",
            "A simple tomato soup",
            [],
            ["4 tomatoes", "1 cup stock"],
            ["Chop tomatoes", "Simmer soup"],
            null,
            null,
            [],
            null,
            null,
            null,
            null
        );

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

    private static Recipe MinimalRecipe(
        string? name = null,
        string? description = null,
        IReadOnlyList<string>? ingredientTexts = null,
        IReadOnlyList<string>? instructionTexts = null
    ) =>
        Recipe.FromImport(
            name,
            description,
            [],
            ingredientTexts ?? [],
            instructionTexts ?? [],
            null,
            null,
            [],
            null,
            null,
            null,
            null
        );

    private static string JoinLines(params string[] lines) => string.Join("\n", lines);
}
