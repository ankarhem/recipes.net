using App.Recipe;
using AwesomeAssertions;
using Schema.NET;
using Xunit;
using DomainRecipe = Domain.Recipe.Recipe;
using DomainRecipeIngredient = Domain.Recipe.RecipeIngredient;
using DomainRecipeInstruction = Domain.Recipe.RecipeInstruction;
using SchemaRecipe = Schema.NET.Recipe;

namespace App.UnitTests;

public class RecipeFactoryTests
{
    [Fact]
    public void FromSchema_FullRecipe_MapsAllFields()
    {
        var firstImageUrl = new Uri("https://example.com/images/soup-one.jpg");
        var secondImageUrl = new Uri("https://example.com/images/soup-two.jpg");
        var schemaRecipe = new SchemaRecipe
        {
            Name = "Tomato Soup",
            Description = "A simple tomato soup",
            Image = new[] { firstImageUrl, secondImageUrl },
            RecipeIngredient = new[] { "4 tomatoes", "1 cup stock" },
            RecipeInstructions = new[] { "Chop tomatoes", "Simmer soup" },
        };

        DomainRecipe result = RecipeFactory.FromSchema(schemaRecipe);

        result.Name.Should().Be("Tomato Soup");
        result.Description.Should().Be("A simple tomato soup");
        result.ImageUrls.Should().Equal(firstImageUrl.ToString(), secondImageUrl.ToString());
        result
            .Ingredients.Should()
            .Equal(
                new DomainRecipeIngredient("4 tomatoes"),
                new DomainRecipeIngredient("1 cup stock")
            );
        result
            .Instructions.Should()
            .Equal(
                new DomainRecipeInstruction(1, "Chop tomatoes"),
                new DomainRecipeInstruction(2, "Simmer soup")
            );
    }

    [Fact]
    public void FromSchema_MultiValueName_TakesFirst()
    {
        var schemaRecipe = new SchemaRecipe { Name = new[] { "Primary Name", "Alternate Name" } };

        DomainRecipe result = RecipeFactory.FromSchema(schemaRecipe);

        result.Name.Should().Be("Primary Name");
    }

    [Fact]
    public void FromSchema_NullName_ReturnsNullName()
    {
        var schemaRecipe = new SchemaRecipe();

        DomainRecipe result = RecipeFactory.FromSchema(schemaRecipe);

        result.Name.Should().BeNull();
    }

    [Fact]
    public void FromSchema_DescriptionNonString_ReturnsNull()
    {
        var schemaRecipe = new SchemaRecipe
        {
            Description = new object[] { new TextObject { Text = "Structured description" } },
        };

        DomainRecipe result = RecipeFactory.FromSchema(schemaRecipe);

        result.Description.Should().BeNull();
    }

    [Fact]
    public void FromSchema_ImagesWithNonUriValues_FiltersToUriOnly()
    {
        var uriImage = new Uri("https://example.com/images/direct.jpg");
        var schemaRecipe = new SchemaRecipe
        {
            Image = new object[]
            {
                new ImageObject { Url = new Uri("https://example.com/images/object.jpg") },
                uriImage,
            },
        };

        DomainRecipe result = RecipeFactory.FromSchema(schemaRecipe);

        result.ImageUrls.Should().ContainSingle(uriImage.ToString());
    }

    [Fact]
    public void FromSchema_InstructionsWithNonStringValues_FiltersToStringOnly()
    {
        var schemaRecipe = new SchemaRecipe
        {
            RecipeInstructions = new object[]
            {
                new CreativeWork { Name = "Structured prep" },
                "Mix batter",
                new ItemList { Name = "Structured list" },
                "Bake cake",
            },
        };

        DomainRecipe result = RecipeFactory.FromSchema(schemaRecipe);

        result
            .Instructions.Should()
            .Equal(
                new DomainRecipeInstruction(1, "Mix batter"),
                new DomainRecipeInstruction(2, "Bake cake")
            );
    }

    [Fact]
    public void FromSchema_Instructions_GetOneBasedPositions()
    {
        var schemaRecipe = new SchemaRecipe
        {
            RecipeInstructions = new[] { "Prep ingredients", "Cook filling", "Serve" },
        };

        DomainRecipe result = RecipeFactory.FromSchema(schemaRecipe);

        result
            .Instructions.Should()
            .Equal(
                new DomainRecipeInstruction(1, "Prep ingredients"),
                new DomainRecipeInstruction(2, "Cook filling"),
                new DomainRecipeInstruction(3, "Serve")
            );
    }

    [Fact]
    public void FromSchema_EmptyCollections_ReturnsEmptyDomainCollections()
    {
        var schemaRecipe = new SchemaRecipe
        {
            Image = Array.Empty<Uri>(),
            RecipeIngredient = Array.Empty<string>(),
            RecipeInstructions = Array.Empty<string>(),
        };

        DomainRecipe result = RecipeFactory.FromSchema(schemaRecipe);

        result.ImageUrls.Should().BeEmpty();
        result.Ingredients.Should().BeEmpty();
        result.Instructions.Should().BeEmpty();
    }

    [Fact]
    public void FromSchema_Ingredients_MapsToTextProperty()
    {
        var schemaRecipe = new SchemaRecipe
        {
            RecipeIngredient = new[] { "1 tsp salt", "2 tbsp olive oil" },
        };

        DomainRecipe result = RecipeFactory.FromSchema(schemaRecipe);

        result
            .Ingredients.Should()
            .Equal(
                new DomainRecipeIngredient("1 tsp salt"),
                new DomainRecipeIngredient("2 tbsp olive oil")
            );
        result
            .Ingredients.Select(ingredient => ingredient.Text)
            .Should()
            .Equal("1 tsp salt", "2 tbsp olive oil");
    }
}
