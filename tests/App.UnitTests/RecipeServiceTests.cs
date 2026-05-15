using App.Embedding;
using App.Recipes;
using AwesomeAssertions;
using Domain.Recipes;
using Microsoft.Extensions.AI;
using NSubstitute;
using Xunit;

namespace App.UnitTests;

public class RecipeServiceTests
{
    private static readonly Recipe SampleRecipe = new()
    {
        Id = Guid.NewGuid(),
        Name = "Pasta Carbonara",
        Description = "A classic Italian dish",
        ImageUrls = Array.Empty<string>(),
        Ingredients = [new RecipeIngredient { Text = "200g spaghetti" }],
        Instructions =
        [
            new RecipeInstruction { Position = 1, Text = "Cook the pasta" },
        ],
    };

    [Fact]
    public async Task SearchRecipesAsync_GeneratesEmbeddingAndCallsRepository()
    {
        var repository = Substitute.For<IRecipeRepository>();
        var generator = Substitute.For<IEmbeddingGenerator<string, Embedding<float>>>();

        var embeddingVector = new float[1536];
        embeddingVector[0] = 0.5f;

        generator
            .GenerateAsync(default!, default!, default)
            .ReturnsForAnyArgs(
                (Func<NSubstitute.Core.CallInfo, Task<GeneratedEmbeddings<Embedding<float>>>>)(
                    _ =>
                        Task.FromResult(
                            new GeneratedEmbeddings<Embedding<float>>([
                                new Embedding<float>(embeddingVector),
                            ])
                        )
                )
            );

        repository
            .SearchAsync(default!, default!, default!, default!, default)
            .ReturnsForAnyArgs(
                (Func<NSubstitute.Core.CallInfo, Task<IReadOnlyList<Recipe>>>)(
                    _ => Task.FromResult<IReadOnlyList<Recipe>>([SampleRecipe])
                )
            );

        var service = new RecipeService(repository, generator);

        var results = await service.SearchRecipesAsync("pasta", 5);

        results.Should().HaveCount(1);
        results[0].Id.Should().Be(SampleRecipe.Id);
        results[0].Name.Should().Be("Pasta Carbonara");

        await generator
            .Received(1)
            .GenerateAsync(
                Arg.Is<IEnumerable<string>>(texts =>
                    texts.Count() == 1 && texts.First() == "pasta"
                ),
                Arg.Is<EmbeddingGenerationOptions>(o => o.Dimensions == 1536),
                Arg.Any<CancellationToken>()
            );

        await repository
            .Received(1)
            .SearchAsync(
                Arg.Any<ReadOnlyMemory<float>>(),
                "text-embedding-3-small",
                1536,
                5,
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task SearchRecipesAsync_EmptyResults_ReturnsEmptyList()
    {
        var repository = Substitute.For<IRecipeRepository>();
        var generator = Substitute.For<IEmbeddingGenerator<string, Embedding<float>>>();

        var embeddingVector = new float[1536];

        generator
            .GenerateAsync(default!, default!, default)
            .ReturnsForAnyArgs(
                (Func<NSubstitute.Core.CallInfo, Task<GeneratedEmbeddings<Embedding<float>>>>)(
                    _ =>
                        Task.FromResult(
                            new GeneratedEmbeddings<Embedding<float>>([
                                new Embedding<float>(embeddingVector),
                            ])
                        )
                )
            );

        repository
            .SearchAsync(default!, default!, default!, default!, default)
            .ReturnsForAnyArgs(
                (Func<NSubstitute.Core.CallInfo, Task<IReadOnlyList<Recipe>>>)(
                    _ => Task.FromResult<IReadOnlyList<Recipe>>([])
                )
            );

        var service = new RecipeService(repository, generator);

        var results = await service.SearchRecipesAsync("nonexistent recipe");

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchRecipesAsync_DefaultLimitIs10()
    {
        var repository = Substitute.For<IRecipeRepository>();
        var generator = Substitute.For<IEmbeddingGenerator<string, Embedding<float>>>();

        var embeddingVector = new float[1536];

        generator
            .GenerateAsync(default!, default!, default)
            .ReturnsForAnyArgs(
                (Func<NSubstitute.Core.CallInfo, Task<GeneratedEmbeddings<Embedding<float>>>>)(
                    _ =>
                        Task.FromResult(
                            new GeneratedEmbeddings<Embedding<float>>([
                                new Embedding<float>(embeddingVector),
                            ])
                        )
                )
            );

        repository
            .SearchAsync(default!, default!, default!, default!, default)
            .ReturnsForAnyArgs(
                (Func<NSubstitute.Core.CallInfo, Task<IReadOnlyList<Recipe>>>)(
                    _ => Task.FromResult<IReadOnlyList<Recipe>>([])
                )
            );

        var service = new RecipeService(repository, generator);

        await service.SearchRecipesAsync("test");

        await repository
            .Received(1)
            .SearchAsync(
                Arg.Any<ReadOnlyMemory<float>>(),
                Arg.Any<string>(),
                Arg.Any<int>(),
                10,
                Arg.Any<CancellationToken>()
            );
    }
}