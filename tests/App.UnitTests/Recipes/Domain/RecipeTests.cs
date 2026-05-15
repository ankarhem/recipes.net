using AwesomeAssertions;
using Domain.Recipes;
using Xunit;

namespace App.UnitTests;

public class RecipeTests
{
    [Fact]
    public void FromImport_WithPrimitiveFields_CreatesRecipeAggregate()
    {
        var recipe = Recipe.FromImport(
            "Tomato Soup",
            "A simple tomato soup",
            ["https://example.com/images/soup.jpg"],
            ["4 tomatoes", "1 cup stock"],
            ["Chop tomatoes", "Simmer soup"],
            "Soup",
            "Italian",
            [DietType.Vegetarian, DietType.GlutenFree],
            TimeSpan.FromMinutes(10),
            TimeSpan.FromMinutes(30),
            TimeSpan.FromMinutes(40),
            4
        );

        recipe.Id.Should().NotBeEmpty();
        recipe.Name.Should().Be("Tomato Soup");
        recipe.Description.Should().Be("A simple tomato soup");
        recipe.ImageUrls.Should().Equal("https://example.com/images/soup.jpg");
        recipe.Category.Should().Be("Soup");
        recipe.Cuisine.Should().Be("Italian");
        recipe.SuitableForDiets.Should().Equal(DietType.Vegetarian, DietType.GlutenFree);
        recipe.PrepTime.Should().Be(TimeSpan.FromMinutes(10));
        recipe.CookTime.Should().Be(TimeSpan.FromMinutes(30));
        recipe.TotalTime.Should().Be(TimeSpan.FromMinutes(40));
        recipe.ServingsCount.Should().Be(4);
        recipe
            .Ingredients.Should()
            .Equal(
                new RecipeIngredient { Text = "4 tomatoes" },
                new RecipeIngredient { Text = "1 cup stock" }
            );
        recipe
            .Instructions.Should()
            .Equal(
                new RecipeInstruction { Position = 1, Text = "Chop tomatoes" },
                new RecipeInstruction { Position = 2, Text = "Simmer soup" }
            );
    }

    [Fact]
    public void FromImport_Instructions_AssignsSequentialOneBasedPositions()
    {
        var recipe = Recipe.FromImport(
            "Layer Cake",
            null,
            [],
            [],
            ["Prep ingredients", "Bake layers", "Frost cake"],
            null,
            null,
            [],
            null,
            null,
            null,
            null
        );

        recipe
            .Instructions.Should()
            .Equal(
                new RecipeInstruction { Position = 1, Text = "Prep ingredients" },
                new RecipeInstruction { Position = 2, Text = "Bake layers" },
                new RecipeInstruction { Position = 3, Text = "Frost cake" }
            );
    }

    [Fact]
    public void FromImport_WithNullNameAndDescription_AllowsNullValues()
    {
        var recipe = Recipe.FromImport(
            null,
            null,
            [],
            ["1 tsp salt"],
            ["Season food"],
            null,
            null,
            [],
            null,
            null,
            null,
            null
        );

        recipe.Name.Should().BeNull();
        recipe.Description.Should().BeNull();
        recipe.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void FromImport_WithEmptyCollections_CreatesEmptyChildCollections()
    {
        var recipe = Recipe.FromImport(
            "Empty Recipe",
            "No details yet",
            [],
            [],
            [],
            null,
            null,
            [],
            null,
            null,
            null,
            null
        );

        recipe.ImageUrls.Should().BeEmpty();
        recipe.SuitableForDiets.Should().BeEmpty();
        recipe.Ingredients.Should().BeEmpty();
        recipe.Instructions.Should().BeEmpty();
    }

    [Fact]
    public void FromImport_WithNullOptionalFilteringFields_AllowsNullValues()
    {
        var recipe = Recipe.FromImport(
            "Simple Cake",
            null,
            [],
            [],
            [],
            null,
            null,
            [],
            null,
            null,
            null,
            null
        );

        recipe.Category.Should().BeNull();
        recipe.Cuisine.Should().BeNull();
        recipe.SuitableForDiets.Should().BeEmpty();
        recipe.PrepTime.Should().BeNull();
        recipe.CookTime.Should().BeNull();
        recipe.TotalTime.Should().BeNull();
        recipe.ServingsCount.Should().BeNull();
    }
}
