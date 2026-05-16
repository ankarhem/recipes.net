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

        recipe.Id.Value.Should().NotBeEmpty();
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
        recipe.DisplayName.Should().Be("Untitled");
        recipe.Description.Should().BeNull();
        recipe.Id.Value.Should().NotBeEmpty();
    }

    [Fact]
    public void FromImport_WithWhitespaceName_UsesUntitledDisplayName()
    {
        var recipe = Recipe.FromImport(
            "   ",
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

        recipe.Name.Should().Be("   ");
        recipe.DisplayName.Should().Be("Untitled");
    }

    [Fact]
    public void FromImport_WithLongCategoryAndCuisine_TruncatesToAggregateLimit()
    {
        var longCategory = new string('c', Recipe.MaxCategoryCuisineLength + 5);
        var longCuisine = new string('u', Recipe.MaxCategoryCuisineLength + 3);

        var recipe = Recipe.FromImport(
            "Soup",
            null,
            [],
            [],
            [],
            longCategory,
            longCuisine,
            [],
            null,
            null,
            null,
            null
        );

        recipe.Category.Should().HaveLength(Recipe.MaxCategoryCuisineLength);
        recipe.Category.Should().Be(longCategory[..Recipe.MaxCategoryCuisineLength]);
        recipe.Cuisine.Should().HaveLength(Recipe.MaxCategoryCuisineLength);
        recipe.Cuisine.Should().Be(longCuisine[..Recipe.MaxCategoryCuisineLength]);
    }

    [Fact]
    public void FromImport_WithWhitespaceCategoryAndCuisine_NormalizesToNull()
    {
        var recipe = Recipe.FromImport(
            "Soup",
            null,
            [],
            [],
            [],
            "   ",
            "\t",
            [],
            null,
            null,
            null,
            null
        );

        recipe.Category.Should().BeNull();
        recipe.Cuisine.Should().BeNull();
    }

    [Theory]
    [InlineData(-1, null, null, null)]
    [InlineData(null, -1, null, null)]
    [InlineData(null, null, -1, null)]
    [InlineData(null, null, null, -1)]
    public void FromImport_WithNegativeValues_Throws(
        int? prepMinutes,
        int? cookMinutes,
        int? totalMinutes,
        int? servingsCount
    )
    {
        var act = () =>
            Recipe.FromImport(
                "Soup",
                null,
                [],
                [],
                [],
                null,
                null,
                [],
                prepMinutes.HasValue ? TimeSpan.FromMinutes(prepMinutes.Value) : null,
                cookMinutes.HasValue ? TimeSpan.FromMinutes(cookMinutes.Value) : null,
                totalMinutes.HasValue ? TimeSpan.FromMinutes(totalMinutes.Value) : null,
                servingsCount
            );

        act.Should().Throw<ArgumentException>();
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

    [Fact]
    public void UpdateDetails_AppliesAggregateOwnedNormalizationAndValidation()
    {
        var recipe = Recipe.FromImport(
            "Soup",
            null,
            [],
            [],
            [],
            "Starter",
            "Italian",
            [],
            null,
            null,
            null,
            null
        );

        recipe.UpdateDetails(
            null,
            "Updated",
            "   ",
            new string('x', Recipe.MaxCategoryCuisineLength + 1),
            2,
            TimeSpan.FromMinutes(5),
            null,
            TimeSpan.FromMinutes(10)
        );

        recipe.Name.Should().BeNull();
        recipe.DisplayName.Should().Be("Untitled");
        recipe.Description.Should().Be("Updated");
        recipe.Category.Should().BeNull();
        recipe.Cuisine.Should().HaveLength(Recipe.MaxCategoryCuisineLength);
        recipe.ServingsCount.Should().Be(2);
        recipe.PrepTime.Should().Be(TimeSpan.FromMinutes(5));
        recipe.CookTime.Should().BeNull();
        recipe.TotalTime.Should().Be(TimeSpan.FromMinutes(10));
    }

    [Fact]
    public void UpdateDetails_WithInvalidValue_DoesNotPartiallyMutateRecipe()
    {
        var recipe = Recipe.FromImport(
            "Soup",
            "Original description",
            [],
            [],
            [],
            "Starter",
            "Italian",
            [],
            TimeSpan.FromMinutes(5),
            null,
            null,
            2
        );

        var act = () =>
            recipe.UpdateDetails(
                "Updated soup",
                "Updated description",
                "Updated category",
                "Updated cuisine",
                -1,
                TimeSpan.FromMinutes(10),
                null,
                null
            );

        act.Should().Throw<ArgumentException>();
        recipe.Name.Should().Be("Soup");
        recipe.Description.Should().Be("Original description");
        recipe.Category.Should().Be("Starter");
        recipe.Cuisine.Should().Be("Italian");
        recipe.ServingsCount.Should().Be(2);
        recipe.PrepTime.Should().Be(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void ReplaceIngredientsAndInstructions_RebuildsChildCollectionsThroughAggregate()
    {
        var recipe = Recipe.FromImport(
            "Soup",
            null,
            [],
            ["old ingredient"],
            ["old step"],
            null,
            null,
            [],
            null,
            null,
            null,
            null
        );

        recipe.ReplaceIngredients(["new ingredient", "salt"]);
        recipe.ReplaceInstructions(["Prep", "Cook"]);

        recipe.Ingredients.Select(i => i.Text).Should().Equal("new ingredient", "salt");
        recipe
            .Instructions.Should()
            .Equal(
                new RecipeInstruction { Position = 1, Text = "Prep" },
                new RecipeInstruction { Position = 2, Text = "Cook" }
            );
    }
}
