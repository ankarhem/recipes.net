using AwesomeAssertions;
using Domain.Recipes;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;
using Xunit;

namespace App.Tests;

public class RecipeConfigurationTests
{
    [Fact]
    public void AppDbContext_ConfiguresRecipeWithOwnedCollections()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=recipes_test;Username=test;Password=test",
                npgsql => npgsql.UseVector()
            )
            .Options;

        using var db = new AppDbContext(options);

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
