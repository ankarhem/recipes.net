using AwesomeAssertions;
using Domain.Recipes;
using Infrastructure.Recipes;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;
using Xunit;

namespace App.Tests;

public class RecipeConfigurationTests
{
    [Fact]
    public void RecipesDbContext_ConfiguresRecipeWithOwnedCollections()
    {
        var options = new DbContextOptionsBuilder<RecipesDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=recipes_test;Username=test;Password=test",
                npgsql => npgsql.UseVector()
            )
            .Options;

        using var db = new RecipesDbContext(options);

        var recipeEntity = db.Model.FindEntityType(typeof(Recipe))!;
        recipeEntity.Should().NotBeNull();

        var ingredientsNav = recipeEntity.FindNavigation(nameof(Recipe.Ingredients))!;
        ingredientsNav.Should().NotBeNull();
        ingredientsNav.IsCollection.Should().BeTrue();

        var instructionsNav = recipeEntity.FindNavigation(nameof(Recipe.Instructions))!;
        instructionsNav.Should().NotBeNull();
        instructionsNav.IsCollection.Should().BeTrue();
}
}
