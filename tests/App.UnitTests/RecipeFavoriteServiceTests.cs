using App.Recipe;
using AwesomeAssertions;
using NSubstitute;
using NSubstitute.Core;
using Xunit;
using DomainRecipeFavorite = Domain.Recipe.RecipeFavorite;

namespace App.UnitTests;

public class RecipeFavoriteServiceTests
{
    [Fact]
    public async Task ToggleFavoriteAsync_UnknownRecipe_ReturnsRecipeNotFound()
    {
        var favoriteRepository = Substitute.For<IRecipeFavoriteRepository>();
        var recipeRepository = Substitute.For<IRecipeRepository>();
        var userId = Guid.NewGuid();
        var recipeId = Guid.NewGuid();

        recipeRepository
            .ExistsAsync(default, default)
            .ReturnsForAnyArgs((Func<CallInfo, Task<bool>>)(_ => Task.FromResult(false)));
        var sut = new RecipeFavoriteService(favoriteRepository, recipeRepository);

        var result = await sut.ToggleFavoriteAsync(userId, recipeId);

        result.Should().BeOfType<ToggleRecipeFavoriteResult.RecipeNotFound>();
        await favoriteRepository.DidNotReceiveWithAnyArgs().FindAsync(default, default, default);
        await favoriteRepository.DidNotReceiveWithAnyArgs().AddAsync(default, default, default);
        await favoriteRepository.DidNotReceiveWithAnyArgs().RemoveAsync(default, default, default);
    }

    [Fact]
    public async Task ToggleFavoriteAsync_NoExistingFavorite_AddsFavorite()
    {
        var favoriteRepository = Substitute.For<IRecipeFavoriteRepository>();
        var recipeRepository = Substitute.For<IRecipeRepository>();
        var userId = Guid.NewGuid();
        var recipeId = Guid.NewGuid();

        recipeRepository
            .ExistsAsync(default, default)
            .ReturnsForAnyArgs((Func<CallInfo, Task<bool>>)(_ => Task.FromResult(true)));
        favoriteRepository
            .FindAsync(default, default, default)
            .ReturnsForAnyArgs(
                (Func<CallInfo, Task<DomainRecipeFavorite?>>)(
                    _ => Task.FromResult<DomainRecipeFavorite?>(null)
                )
            );
        favoriteRepository.AddAsync(default, default, default).ReturnsForAnyArgs(Task.CompletedTask);
        var sut = new RecipeFavoriteService(favoriteRepository, recipeRepository);

        var result = await sut.ToggleFavoriteAsync(userId, recipeId);

        var success = result.Should().BeOfType<ToggleRecipeFavoriteResult.Success>().Subject;
        success.IsFavorite.Should().BeTrue();
        await favoriteRepository.Received(1).AddAsync(userId, recipeId, Arg.Any<CancellationToken>());
        await favoriteRepository.DidNotReceiveWithAnyArgs().RemoveAsync(default, default, default);
    }

    [Fact]
    public async Task ToggleFavoriteAsync_ExistingFavorite_RemovesFavorite()
    {
        var favoriteRepository = Substitute.For<IRecipeFavoriteRepository>();
        var recipeRepository = Substitute.For<IRecipeRepository>();
        var userId = Guid.NewGuid();
        var recipeId = Guid.NewGuid();
        var favorite = new DomainRecipeFavorite
        {
            UserId = userId,
            RecipeId = recipeId,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        recipeRepository
            .ExistsAsync(default, default)
            .ReturnsForAnyArgs((Func<CallInfo, Task<bool>>)(_ => Task.FromResult(true)));
        favoriteRepository
            .FindAsync(default, default, default)
            .ReturnsForAnyArgs(
                (Func<CallInfo, Task<DomainRecipeFavorite?>>)(
                    _ => Task.FromResult<DomainRecipeFavorite?>(favorite)
                )
            );
        favoriteRepository.RemoveAsync(default, default, default).ReturnsForAnyArgs(Task.CompletedTask);
        var sut = new RecipeFavoriteService(favoriteRepository, recipeRepository);

        var result = await sut.ToggleFavoriteAsync(userId, recipeId);

        var success = result.Should().BeOfType<ToggleRecipeFavoriteResult.Success>().Subject;
        success.IsFavorite.Should().BeFalse();
        await favoriteRepository.Received(1).RemoveAsync(userId, recipeId, Arg.Any<CancellationToken>());
        await favoriteRepository.DidNotReceiveWithAnyArgs().AddAsync(default, default, default);
    }

    [Fact]
    public async Task ListFavoriteRecipeIdsAsync_ReturnsRepositoryResults()
    {
        var favoriteRepository = Substitute.For<IRecipeFavoriteRepository>();
        var recipeRepository = Substitute.For<IRecipeRepository>();
        var userId = Guid.NewGuid();
        IReadOnlyList<Guid> favoriteIds = [Guid.NewGuid(), Guid.NewGuid()];

        favoriteRepository
            .ListByUserIdAsync(default, default)
            .ReturnsForAnyArgs(
                (Func<CallInfo, Task<IReadOnlyList<Guid>>>)
                    (_ => Task.FromResult(favoriteIds))
            );
        var sut = new RecipeFavoriteService(favoriteRepository, recipeRepository);

        var result = await sut.ListFavoriteRecipeIdsAsync(userId);

        result.Should().Equal(favoriteIds);
        await favoriteRepository.Received(1).ListByUserIdAsync(userId, Arg.Any<CancellationToken>());
    }
}
