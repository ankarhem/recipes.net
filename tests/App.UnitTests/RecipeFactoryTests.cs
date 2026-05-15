using App.Recipes;
using AwesomeAssertions;
using Domain.Recipes;
using Xunit;

namespace App.UnitTests;

public class RecipeFactoryTests
{
    [Fact]
    public void FromSchema_FullRecipe_MapsAllFields()
    {
        var firstImageUrl = new Uri("https://example.com/images/soup-one.jpg");
        var secondImageUrl = new Uri("https://example.com/images/soup-two.jpg");
        var schemaRecipe = new Schema.NET.Recipe
        {
            Name = "Tomato Soup",
            Description = "A simple tomato soup",
            Image = new[] { firstImageUrl, secondImageUrl },
            RecipeIngredient = new[] { "4 tomatoes", "1 cup stock" },
            RecipeInstructions = new[] { "Chop tomatoes", "Simmer soup" },
        };

        Recipe result = RecipeFactory.FromSchema(schemaRecipe);

        result.Name.Should().Be("Tomato Soup");
        result.Description.Should().Be("A simple tomato soup");
        result.ImageUrls.Should().Equal(firstImageUrl.ToString(), secondImageUrl.ToString());
        result
            .Ingredients.Should()
            .Equal(
                new RecipeIngredient { Text = "4 tomatoes" },
                new RecipeIngredient { Text = "1 cup stock" }
            );
        result
            .Instructions.Should()
            .Equal(
                new RecipeInstruction { Position = 1, Text = "Chop tomatoes" },
                new RecipeInstruction { Position = 2, Text = "Simmer soup" }
            );
    }

    [Fact]
    public void FromSchema_MultiValueName_TakesFirst()
    {
        var schemaRecipe = new Schema.NET.Recipe { Name = new[] { "Primary Name", "Alternate Name" } };

        Recipe result = RecipeFactory.FromSchema(schemaRecipe);

        result.Name.Should().Be("Primary Name");
    }

    [Fact]
    public void FromSchema_NullName_ReturnsNullName()
    {
        var schemaRecipe = new Schema.NET.Recipe();

        Recipe result = RecipeFactory.FromSchema(schemaRecipe);

        result.Name.Should().BeNull();
    }

    [Fact]
    public void FromSchema_DescriptionNonString_ReturnsNull()
    {
        var schemaRecipe = new Schema.NET.Recipe
        {
            Description = new object[] { new Schema.NET.TextObject { Text = "Structured description" } },
        };

        Recipe result = RecipeFactory.FromSchema(schemaRecipe);

        result.Description.Should().BeNull();
    }

    [Fact]
    public void FromSchema_ImagesWithNonUriValues_FiltersToUriOnly()
    {
        var uriImage = new Uri("https://example.com/images/direct.jpg");
        var schemaRecipe = new Schema.NET.Recipe
        {
            Image = new object[]
            {
                new Schema.NET.ImageObject { Url = new Uri("https://example.com/images/object.jpg") },
                uriImage,
            },
        };

        Recipe result = RecipeFactory.FromSchema(schemaRecipe);

        result.ImageUrls.Should().ContainSingle(uriImage.ToString());
    }

    [Fact]
    public void FromSchema_InstructionsWithNonStringValues_FiltersToStringOnly()
    {
        var schemaRecipe = new Schema.NET.Recipe
        {
            RecipeInstructions = new object[]
            {
                new Schema.NET.CreativeWork { Name = "Structured prep" },
                "Mix batter",
                new Schema.NET.ItemList { Name = "Structured list" },
                "Bake cake",
            },
        };

        Recipe result = RecipeFactory.FromSchema(schemaRecipe);

        result
            .Instructions.Should()
            .Equal(
                new RecipeInstruction { Position = 1, Text = "Mix batter" },
                new RecipeInstruction { Position = 2, Text = "Bake cake" }
            );
    }

    [Fact]
    public void FromSchema_Instructions_GetOneBasedPositions()
    {
        var schemaRecipe = new Schema.NET.Recipe
        {
            RecipeInstructions = new[] { "Prep ingredients", "Cook filling", "Serve" },
        };

        Recipe result = RecipeFactory.FromSchema(schemaRecipe);

        result
            .Instructions.Should()
            .Equal(
                new RecipeInstruction { Position = 1, Text = "Prep ingredients" },
                new RecipeInstruction { Position = 2, Text = "Cook filling" },
                new RecipeInstruction { Position = 3, Text = "Serve" }
            );
    }

    [Fact]
    public void FromSchema_EmptyCollections_ReturnsEmptyDomainCollections()
    {
        var schemaRecipe = new Schema.NET.Recipe
        {
            Image = Array.Empty<Uri>(),
            RecipeIngredient = Array.Empty<string>(),
            RecipeInstructions = Array.Empty<string>(),
        };

        Recipe result = RecipeFactory.FromSchema(schemaRecipe);

        result.ImageUrls.Should().BeEmpty();
        result.Ingredients.Should().BeEmpty();
        result.Instructions.Should().BeEmpty();
    }

    [Fact]
    public void FromSchema_Ingredients_MapsToTextProperty()
    {
        var schemaRecipe = new Schema.NET.Recipe
        {
            RecipeIngredient = new[] { "1 tsp salt", "2 tbsp olive oil" },
        };

        Recipe result = RecipeFactory.FromSchema(schemaRecipe);

        result
            .Ingredients.Should()
            .Equal(
                new RecipeIngredient { Text = "1 tsp salt" },
                new RecipeIngredient { Text = "2 tbsp olive oil" }
            );
        result
            .Ingredients.Select(ingredient => ingredient.Text)
            .Should()
            .Equal("1 tsp salt", "2 tbsp olive oil");
    }
}
