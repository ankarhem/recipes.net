using App.Recipes;
using App.Recipes.Ports;
using AwesomeAssertions;
using Domain;
using Domain.Recipes;
using NSubstitute;
using NSubstitute.Core;
using Xunit;

namespace App.UnitTests;

public class RecipeCollectionServiceTests
{
    [Fact]
    public async Task ToggleFavoriteAsync_UnknownRecipe_ReturnsRecipeNotFound()
    {
        var collectionRepository = Substitute.For<IRecipeCollectionRepository>();
        var recipeRepository = Substitute.For<IRecipeRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var clock = Substitute.For<IClock>();
        var userId = Guid.NewGuid();
        var recipeId = Guid.NewGuid();

        recipeRepository
            .ExistsAsync(default, default)
            .ReturnsForAnyArgs((Func<CallInfo, Task<bool>>)(_ => Task.FromResult(false)));
        var sut = new RecipeCollectionService(
            collectionRepository,
            recipeRepository,
            unitOfWork,
            clock
        );

        var result = await sut.ToggleFavoriteAsync(userId, recipeId);

        result.Should().BeOfType<ToggleRecipeResult.RecipeNotFound>();
        await collectionRepository
            .DidNotReceiveWithAnyArgs()
            .GetDefaultFavoritesAsync(default, default);
        await collectionRepository.DidNotReceiveWithAnyArgs().SaveAsync(default!, default);
        await unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task ToggleRecipeAsync_UnknownCollection_ReturnsCollectionNotFound()
    {
        var collectionRepository = Substitute.For<IRecipeCollectionRepository>();
        var recipeRepository = Substitute.For<IRecipeRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var clock = Substitute.For<IClock>();
        var collectionId = Guid.NewGuid();
        var recipeId = Guid.NewGuid();

        collectionRepository
            .GetByIdAsync(default, default)
            .ReturnsForAnyArgs(
                (Func<CallInfo, Task<RecipeCollection?>>)(
                    _ => Task.FromResult<RecipeCollection?>(null)
                )
            );
        var sut = new RecipeCollectionService(
            collectionRepository,
            recipeRepository,
            unitOfWork,
            clock
        );

        var result = await sut.ToggleRecipeAsync(collectionId, recipeId);

        result.Should().BeOfType<ToggleRecipeResult.CollectionNotFound>();
        await recipeRepository.DidNotReceiveWithAnyArgs().ExistsAsync(default, default);
        await collectionRepository.DidNotReceiveWithAnyArgs().SaveAsync(default!, default);
        await unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task ToggleFavoriteAsync_NoExistingFavorite_AddsRecipeToDefaultFavoritesCollection()
    {
        var collectionRepository = Substitute.For<IRecipeCollectionRepository>();
        var recipeRepository = Substitute.For<IRecipeRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var clock = Substitute.For<IClock>();
        var userId = Guid.NewGuid();
        var recipeId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        clock.UtcNow.Returns(now);

        recipeRepository
            .ExistsAsync(default, default)
            .ReturnsForAnyArgs((Func<CallInfo, Task<bool>>)(_ => Task.FromResult(true)));
        collectionRepository
            .GetDefaultFavoritesAsync(default, default)
            .ReturnsForAnyArgs(
                (Func<CallInfo, Task<RecipeCollection?>>)(
                    _ => Task.FromResult<RecipeCollection?>(null)
                )
            );
        collectionRepository.SaveAsync(default!, default).ReturnsForAnyArgs(Task.CompletedTask);
        unitOfWork.SaveChangesAsync(default).ReturnsForAnyArgs(Task.CompletedTask);
        var sut = new RecipeCollectionService(
            collectionRepository,
            recipeRepository,
            unitOfWork,
            clock
        );

        var result = await sut.ToggleFavoriteAsync(userId, recipeId);

        var success = result.Should().BeOfType<ToggleRecipeResult.Success>().Subject;
        success.IsAdded.Should().BeTrue();
        await collectionRepository.Received(1).SaveAsync(
            Arg.Is<RecipeCollection>(c =>
                c.OwnerId.Value == userId
                && c.IsDefaultFavorites
                && c.ContainsRecipe(new RecipeId(recipeId))
            ),
            Arg.Any<CancellationToken>()
        );
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ToggleFavoriteAsync_ExistingFavorite_RemovesRecipeFromDefaultFavoritesCollection()
    {
        var collectionRepository = Substitute.For<IRecipeCollectionRepository>();
        var recipeRepository = Substitute.For<IRecipeRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var clock = Substitute.For<IClock>();
        var userId = Guid.NewGuid();
        var recipeId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        clock.UtcNow.Returns(now);
        var collection = RecipeCollection.CreateFavorites(new RecipeCollectionOwnerId(userId), now);
        collection.AddRecipe(new RecipeId(recipeId), now);

        recipeRepository
            .ExistsAsync(default, default)
            .ReturnsForAnyArgs((Func<CallInfo, Task<bool>>)(_ => Task.FromResult(true)));
        collectionRepository
            .GetDefaultFavoritesAsync(default, default)
            .ReturnsForAnyArgs(
                (Func<CallInfo, Task<RecipeCollection?>>)(
                    _ => Task.FromResult<RecipeCollection?>(collection)
                )
            );
        collectionRepository.SaveAsync(default!, default).ReturnsForAnyArgs(Task.CompletedTask);
        unitOfWork.SaveChangesAsync(default).ReturnsForAnyArgs(Task.CompletedTask);
        var sut = new RecipeCollectionService(
            collectionRepository,
            recipeRepository,
            unitOfWork,
            clock
        );

        var result = await sut.ToggleFavoriteAsync(userId, recipeId);

        var success = result.Should().BeOfType<ToggleRecipeResult.Success>().Subject;
        success.IsAdded.Should().BeFalse();
        await collectionRepository.Received(1).SaveAsync(
            Arg.Is<RecipeCollection>(c => !c.ContainsRecipe(new RecipeId(recipeId))),
            Arg.Any<CancellationToken>()
        );
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListFavoriteRecipeIdsAsync_ReturnsDefaultFavoritesCollectionItems()
    {
        var collectionRepository = Substitute.For<IRecipeCollectionRepository>();
        var recipeRepository = Substitute.For<IRecipeRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var clock = Substitute.For<IClock>();
        var userId = Guid.NewGuid();
        var firstRecipeId = Guid.NewGuid();
        var secondRecipeId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var collection = RecipeCollection.CreateFavorites(new RecipeCollectionOwnerId(userId), now);
        collection.AddRecipe(new RecipeId(firstRecipeId), now);
        collection.AddRecipe(new RecipeId(secondRecipeId), now);

        collectionRepository
            .GetDefaultFavoritesAsync(default, default)
            .ReturnsForAnyArgs(
                (Func<CallInfo, Task<RecipeCollection?>>)(
                    _ => Task.FromResult<RecipeCollection?>(collection)
                )
            );
        var sut = new RecipeCollectionService(
            collectionRepository,
            recipeRepository,
            unitOfWork,
            clock
        );

        var result = await sut.ListFavoriteRecipeIdsAsync(userId);

        result.Should().Equal(firstRecipeId, secondRecipeId);
        await collectionRepository
            .Received(1)
            .GetDefaultFavoritesAsync(
                new RecipeCollectionOwnerId(userId),
                Arg.Any<CancellationToken>()
            );
    }
}
