using App.Embedding;
using App.Recipes;
using AwesomeAssertions;
using Domain.Recipes;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace App.UnitTests;

public class EmbeddingServiceTests
{
    private static readonly Recipe SampleRecipe = new()
    {
        Id = Guid.NewGuid(),
        Name = "Tomato Soup",
        Description = "A simple soup",
        ImageUrls = Array.Empty<string>(),
        SuitableForDiets = [],
        Ingredients = [new RecipeIngredient { Text = "2 tomatoes" }],
        Instructions = [new RecipeInstruction { Position = 1, Text = "Chop tomatoes" }],
    };

    private static readonly string ExpectedCanonicalText = new RecipeEmbeddingTextBuilder().Build(
        SampleRecipe
    );

    private static readonly string ExpectedInputHash = RecipeEmbeddingTextBuilder.ComputeInputHash(
        ExpectedCanonicalText
    );

    [Fact]
    public async Task EnsureRecipeEmbeddingAsync_SkipsWhenAlreadyExists()
    {
        var textBuilder = Substitute.For<IRecipeEmbeddingTextBuilder>();
        var repository = Substitute.For<IRecipeEmbeddingRepository>();
        var generator = Substitute.For<IEmbeddingGenerator<string, Embedding<float>>>();

        repository
            .ExistsAsync(default!, default!, default!, default!, default)
            .ReturnsForAnyArgs(true);

        var service = new EmbeddingService(
            textBuilder,
            repository,
            generator,
            NullLogger<EmbeddingService>.Instance
        );

        await service.EnsureRecipeEmbeddingAsync(
            Guid.NewGuid(),
            SampleRecipe,
            EmbeddingModel.TextEmbedding3Small
        );

        await generator.DidNotReceiveWithAnyArgs().GenerateAsync(default!, default!, default);
        await repository
            .DidNotReceiveWithAnyArgs()
            .EnsureEmbeddingAsync(
                default!,
                default!,
                default!,
                default!,
                default!,
                default
            );
    }

    [Fact]
    public async Task EnsureRecipeEmbeddingAsync_GeneratesAndSavesWhenNotExists()
    {
        var textBuilder = new RecipeEmbeddingTextBuilder();
        var repository = Substitute.For<IRecipeEmbeddingRepository>();
        var generator = Substitute.For<IEmbeddingGenerator<string, Embedding<float>>>();

        var embeddingVector = new float[1536];
        embeddingVector[0] = 0.42f;

        repository
            .ExistsAsync(default!, default!, default!, default!, default)
            .ReturnsForAnyArgs(false);

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

        var service = new EmbeddingService(
            textBuilder,
            repository,
            generator,
            NullLogger<EmbeddingService>.Instance
        );

        var recipeId = Guid.NewGuid();

        await service.EnsureRecipeEmbeddingAsync(
            recipeId,
            SampleRecipe,
            EmbeddingModel.TextEmbedding3Small
        );

        await repository
            .Received(1)
            .EnsureEmbeddingAsync(
                recipeId,
                "text-embedding-3-small",
                1536,
                ExpectedInputHash,
                Arg.Any<ReadOnlyMemory<float>>(),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task EnsureRecipeEmbeddingAsync_UsesCanonicalTextForHash()
    {
        var textBuilder = new RecipeEmbeddingTextBuilder();
        var repository = Substitute.For<IRecipeEmbeddingRepository>();
        var generator = Substitute.For<IEmbeddingGenerator<string, Embedding<float>>>();

        repository
            .ExistsAsync(default!, default!, default!, default!, default)
            .ReturnsForAnyArgs(false);

        generator
            .GenerateAsync(default!, default!, default)
            .ReturnsForAnyArgs(
                (Func<NSubstitute.Core.CallInfo, Task<GeneratedEmbeddings<Embedding<float>>>>)(
                    _ =>
                        Task.FromResult(
                            new GeneratedEmbeddings<Embedding<float>>([
                                new Embedding<float>(new float[1536]),
                            ])
                        )
                )
            );

        var service = new EmbeddingService(
            textBuilder,
            repository,
            generator,
            NullLogger<EmbeddingService>.Instance
        );

        await service.EnsureRecipeEmbeddingAsync(
            Guid.NewGuid(),
            SampleRecipe,
            EmbeddingModel.TextEmbedding3Small
        );

        await generator
            .Received(1)
            .GenerateAsync(
                Arg.Is<IEnumerable<string>>(texts =>
                    texts.Count() == 1 && texts.First() == ExpectedCanonicalText
                ),
                Arg.Is<EmbeddingGenerationOptions>(o => o.Dimensions == 1536),
                Arg.Any<CancellationToken>()
            );

        await repository
            .Received(1)
            .EnsureEmbeddingAsync(
                Arg.Any<Guid>(),
                "text-embedding-3-small",
                1536,
                ExpectedInputHash,
                Arg.Any<ReadOnlyMemory<float>>(),
                Arg.Any<CancellationToken>()
            );
    }
}
