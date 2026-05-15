using AwesomeAssertions;
using Domain.Recipes;
using Infrastructure.Recipes;
using Xunit;

namespace App.Tests;

public class RecipeEntityTests
{
    [Fact]
    public void ToDomain_MapsAllFieldsFromEntity()
    {
        var entity = new RecipeEntity
        {
            Id = Guid.NewGuid(),
            Url = "https://example.com/recipe",
            Name = "Test Recipe",
            Description = "A test description",
            ImageUrlsJson = """["https://example.com/img.jpg"]""",
            Category = "Dessert",
            Cuisine = "Italian",
            SuitableForDiets = [DietType.Vegetarian, DietType.GlutenFree],
            PrepTime = TimeSpan.FromMinutes(15),
            CookTime = TimeSpan.FromMinutes(45),
            TotalTime = TimeSpan.FromHours(1),
            ServingsCount = 4,
            JsonLd = "{}",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            IngredientEntities =
            [
                new RecipeIngredientEntity
                {
                    Id = Guid.NewGuid(),
                    RecipeId = Guid.NewGuid(),
                    Text = "1 cup flour",
                },
            ],
            InstructionEntities =
            [
                new RecipeInstructionEntity
                {
                    Id = Guid.NewGuid(),
                    RecipeId = Guid.NewGuid(),
                    Position = 2,
                    Text = "Bake",
                    Name = "Oven",
                },
                new RecipeInstructionEntity
                {
                    Id = Guid.NewGuid(),
                    RecipeId = Guid.NewGuid(),
                    Position = 1,
                    Text = "Mix",
                },
            ],
        };

        var recipe = entity.ToDomain();

        recipe.Id.Should().Be(entity.Id);
        recipe.Name.Should().Be("Test Recipe");
        recipe.Description.Should().Be("A test description");
        recipe.ImageUrls.Should().Equal("https://example.com/img.jpg");
        recipe.Category.Should().Be("Dessert");
        recipe.Cuisine.Should().Be("Italian");
        recipe.SuitableForDiets.Should().Equal(DietType.Vegetarian, DietType.GlutenFree);
        recipe.PrepTime.Should().Be(TimeSpan.FromMinutes(15));
        recipe.CookTime.Should().Be(TimeSpan.FromMinutes(45));
        recipe.TotalTime.Should().Be(TimeSpan.FromHours(1));
        recipe.ServingsCount.Should().Be(4);
        recipe.Ingredients.Should().HaveCount(1);
        recipe.Ingredients[0].Text.Should().Be("1 cup flour");
        recipe.Instructions.Should().HaveCount(2);
        recipe.Instructions[0].Position.Should().Be(1);
        recipe.Instructions[0].Text.Should().Be("Mix");
        recipe.Instructions[1].Position.Should().Be(2);
        recipe.Instructions[1].Text.Should().Be("Bake");
        recipe.Instructions[1].Name.Should().Be("Oven");
    }
}
