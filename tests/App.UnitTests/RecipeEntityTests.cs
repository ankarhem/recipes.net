using AwesomeAssertions;
using Domain.Recipes;
using Infrastructure.Recipes;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;
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

        recipe.Id.Should().Be(new RecipeId(entity.Id));
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

    [Fact]
    public void UpdateFromDomain_DiffsCollectionItemsWithoutRecreatingExistingItems()
    {
        var collectionId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var firstRecipeId = Guid.NewGuid();
        var removedRecipeId = Guid.NewGuid();
        var addedRecipeId = Guid.NewGuid();
        var existingItemId = Guid.NewGuid();
        var createdAt = DateTimeOffset.Parse("2026-05-15T12:00:00Z");
        var updatedAt = createdAt.AddMinutes(5);
        var entity = new RecipeCollectionEntity
        {
            Id = collectionId,
            OwnerUserId = ownerId,
            Name = "Favorites",
            Kind = RecipeCollectionKind.Favorites,
            Visibility = RecipeCollectionVisibility.Private,
            CreatedAt = createdAt,
            UpdatedAt = createdAt,
            Items =
            [
                new RecipeCollectionItemEntity
                {
                    Id = existingItemId,
                    CollectionId = collectionId,
                    RecipeId = firstRecipeId,
                    Position = 1,
                    AddedAt = createdAt,
                },
                new RecipeCollectionItemEntity
                {
                    Id = Guid.NewGuid(),
                    CollectionId = collectionId,
                    RecipeId = removedRecipeId,
                    Position = 2,
                    AddedAt = createdAt,
                },
            ],
        };
        var collection = RecipeCollection.Rehydrate(
            new RecipeCollectionId(collectionId),
            new RecipeCollectionOwnerId(ownerId),
            "Favorites",
            RecipeCollectionKind.Favorites,
            RecipeCollectionVisibility.Private,
            createdAt,
            updatedAt,
            [
                new RecipeCollectionItem
                {
                    RecipeId = new RecipeId(firstRecipeId),
                    Position = 1,
                    AddedAt = createdAt,
                },
                new RecipeCollectionItem
                {
                    RecipeId = new RecipeId(addedRecipeId),
                    Position = 2,
                    AddedAt = updatedAt,
                },
            ]
        );

        entity.UpdateFromDomain(collection);

        entity.Items.Should().HaveCount(2);
        entity.Items.Should().ContainSingle(i => i.Id == existingItemId && i.RecipeId == firstRecipeId);
        entity.Items.Should().NotContain(i => i.RecipeId == removedRecipeId);
        entity
            .Items.Should()
            .ContainSingle(i =>
                i.RecipeId == addedRecipeId
                && i.CollectionId == collectionId
                && i.Position == 2
                && i.AddedAt == updatedAt
            );
    }

    [Fact]
    public void RecipesDbContext_DoesNotEnforceUniqueCollectionItemPositions()
    {
        var options = new DbContextOptionsBuilder<RecipesDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=recipes_test;Username=test;Password=test",
                npgsql => npgsql.UseVector()
            )
            .Options;

        using var db = new RecipesDbContext(options);

        var collectionItemIndexes = db.Model.FindEntityType(typeof(RecipeCollectionItemEntity))!
            .GetIndexes()
            .Select(index => new
            {
                Properties = index.Properties.Select(property => property.Name).ToArray(),
                index.IsUnique,
            })
            .ToList();

        collectionItemIndexes.Should()
            .ContainSingle(index =>
                index.IsUnique
                && index.Properties.SequenceEqual(
                    new[]
                    {
                        nameof(RecipeCollectionItemEntity.CollectionId),
                        nameof(RecipeCollectionItemEntity.RecipeId),
                    }
                )
            );
        collectionItemIndexes.Should()
            .NotContain(index =>
                index.IsUnique
                && index.Properties.SequenceEqual(
                    new[]
                    {
                        nameof(RecipeCollectionItemEntity.CollectionId),
                        nameof(RecipeCollectionItemEntity.Position),
                    }
                )
            );
    }
}
