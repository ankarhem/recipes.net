using App.Recipes;
using App.Recipes.Ports;
using AwesomeAssertions;
using Domain.Recipes;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace App.UnitTests;

public class RecipeEmbeddingServiceTests
{
    private static readonly Recipe SampleRecipe = Recipe.FromImport(
        "Tomato Soup",
        "A simple soup",
        [],
        ["2 tomatoes"],
        ["Chop tomatoes"],
        null,
        null,
        [],
        null,
        null,
        null,
        null
    );

    private static readonly string ExpectedCanonicalText = new RecipeEmbeddingTextBuilder().Build(
        SampleRecipe
    );

    private static readonly string ExpectedInputHash = RecipeEmbeddingTextBuilder.ComputeInputHash(
        ExpectedCanonicalText
    );

    private static readonly EmbeddingModel Model = EmbeddingModel.TextEmbedding3Small.Instance;

    [Fact]
    public async Task EnsureRecipeEmbeddingAsync_SkipsWhenAlreadyExists()
    {
        var textBuilder = Substitute.For<IRecipeEmbeddingTextBuilder>();
        var repository = Substitute.For<IRecipeEmbeddingRepository>();
        var generator = Substitute.For<IEmbeddingGenerator<string, Embedding<float>>>();

        repository
            .ExistsAsync(default!, default!, default!, default)
            .ReturnsForAnyArgs(true);

        var unitOfWork = Substitute.For<IUnitOfWork>();
        var service = new RecipeEmbeddingService(
            textBuilder,
            repository,
            unitOfWork,
            generator,
            NullLogger<RecipeEmbeddingService>.Instance
        );

        await service.EnsureRecipeEmbeddingAsync(
            Guid.NewGuid(),
            SampleRecipe,
            Model
        );

        await generator.DidNotReceiveWithAnyArgs().GenerateAsync(default!, default!, default);
        await repository
            .DidNotReceiveWithAnyArgs()
            .EnsureEmbeddingAsync(
                default!,
                default!,
                default!,
                default!,
                default
            );
        await unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
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
            .ExistsAsync(default!, default!, default!, default)
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

        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.SaveChangesAsync(default).ReturnsForAnyArgs(Task.CompletedTask);
        var service = new RecipeEmbeddingService(
            textBuilder,
            repository,
            unitOfWork,
            generator,
            NullLogger<RecipeEmbeddingService>.Instance
        );

        var recipeId = Guid.NewGuid();

        await service.EnsureRecipeEmbeddingAsync(
            recipeId,
            SampleRecipe,
            Model
        );

        await repository
            .Received(1)
            .EnsureEmbeddingAsync(
                recipeId,
                Model,
                ExpectedInputHash,
                Arg.Any<ReadOnlyMemory<float>>(),
                Arg.Any<CancellationToken>()
            );
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnsureRecipeEmbeddingAsync_UsesCanonicalTextForHash()
    {
        var textBuilder = new RecipeEmbeddingTextBuilder();
        var repository = Substitute.For<IRecipeEmbeddingRepository>();
        var generator = Substitute.For<IEmbeddingGenerator<string, Embedding<float>>>();

        repository
            .ExistsAsync(default!, default!, default!, default)
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

        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.SaveChangesAsync(default).ReturnsForAnyArgs(Task.CompletedTask);
        var service = new RecipeEmbeddingService(
            textBuilder,
            repository,
            unitOfWork,
            generator,
            NullLogger<RecipeEmbeddingService>.Instance
        );

        await service.EnsureRecipeEmbeddingAsync(
            Guid.NewGuid(),
            SampleRecipe,
            Model
        );

        await generator
            .Received(1)
            .GenerateAsync(
                Arg.Is<IEnumerable<string>>(texts =>
                    texts.Count() == 1 && texts.First() == ExpectedCanonicalText
                ),
                Arg.Is<EmbeddingGenerationOptions>(o => o.Dimensions == Model.Dimensions),
                Arg.Any<CancellationToken>()
            );

        await repository
            .Received(1)
            .EnsureEmbeddingAsync(
                Arg.Any<Guid>(),
                Model,
                ExpectedInputHash,
                Arg.Any<ReadOnlyMemory<float>>(),
                Arg.Any<CancellationToken>()
            );
    }
}
