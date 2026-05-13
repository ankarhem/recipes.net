using System.Text.Json;
using DomainRecipe = Domain.Recipe.Recipe;
using DomainRecipeIngredient = Domain.Recipe.RecipeIngredient;
using DomainRecipeInstruction = Domain.Recipe.RecipeInstruction;

namespace Infrastructure.Recipe;

public sealed class RecipeEntity
{
    public Guid Id { get; set; }
    public required string Url { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public required string ImageUrlsJson { get; set; } = "[]";
    public required string JsonLd { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public List<RecipeIngredientEntity> IngredientEntities { get; set; } = [];
    public List<RecipeInstructionEntity> InstructionEntities { get; set; } = [];

    public DomainRecipe ToDomain() =>
        new()
        {
            Id = Id,
            Name = Name,
            Description = Description,
            ImageUrls = JsonSerializer.Deserialize<List<string>>(ImageUrlsJson) ?? [],
            Ingredients = IngredientEntities
                .Select(e => new DomainRecipeIngredient { Text = e.Text })
                .ToList(),
            Instructions = InstructionEntities
                .OrderBy(e => e.Position)
                .Select(e => new DomainRecipeInstruction
                {
                    Position = e.Position,
                    Text = e.Text,
                    Name = e.Name,
                })
                .ToList(),
        };

    public static RecipeEntity FromImport(
        DomainRecipe recipe,
        string sourceUrl,
        string rawSchemaJson
    )
    {
        return new RecipeEntity
        {
            Id = recipe.Id,
            Url = sourceUrl,
            Name = recipe.Name ?? "Untitled",
            Description = recipe.Description,
            ImageUrlsJson = JsonSerializer.Serialize(recipe.ImageUrls),
            JsonLd = rawSchemaJson,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            IngredientEntities = recipe
                .Ingredients.Select(i => new RecipeIngredientEntity
                {
                    Id = Guid.NewGuid(),
                    RecipeId = recipe.Id,
                    Text = i.Text,
                })
                .ToList(),
            InstructionEntities = recipe
                .Instructions.Select(i => new RecipeInstructionEntity
                {
                    Id = Guid.NewGuid(),
                    RecipeId = recipe.Id,
                    Position = i.Position,
                    Text = i.Text,
                    Name = i.Name,
                })
                .ToList(),
        };
    }
}
