using System.Text.Json;
using Domain.Recipes;

namespace Infrastructure.Recipes;

public sealed class RecipeEntity
{
    public Guid Id { get; set; }
    public required string Url { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public required string ImageUrlsJson { get; set; } = "[]";
    public string? Category { get; set; }
    public string? Cuisine { get; set; }
    public IReadOnlyList<DietType> SuitableForDiets { get; set; } = [];
    public TimeSpan? PrepTime { get; set; }
    public TimeSpan? CookTime { get; set; }
    public TimeSpan? TotalTime { get; set; }
    public int? ServingsCount { get; set; }
    public required string JsonLd { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public List<RecipeIngredientEntity> IngredientEntities { get; set; } = [];
    public List<RecipeInstructionEntity> InstructionEntities { get; set; } = [];

    public Recipe ToDomain() =>
        new()
        {
            Id = Id,
            Name = Name,
            Description = Description,
            ImageUrls = JsonSerializer.Deserialize<List<string>>(ImageUrlsJson) ?? [],
            Category = Category,
            Cuisine = Cuisine,
            SuitableForDiets = SuitableForDiets,
            PrepTime = PrepTime,
            CookTime = CookTime,
            TotalTime = TotalTime,
            ServingsCount = ServingsCount,
            Ingredients = IngredientEntities
                .Select(e => new RecipeIngredient { Text = e.Text })
                .ToList(),
            Instructions = InstructionEntities
                .OrderBy(e => e.Position)
                .Select(e => new RecipeInstruction
                {
                    Position = e.Position,
                    Text = e.Text,
                    Name = e.Name,
                })
                .ToList(),
        };

    public static RecipeEntity FromImport(
        Recipe recipe,
        string sourceUrl,
        string rawSchemaJson,
        DateTimeOffset now
    )
    {
        return new RecipeEntity
        {
            Id = recipe.Id,
            Url = sourceUrl,
            Name = recipe.Name ?? "Untitled",
            Description = recipe.Description,
            ImageUrlsJson = JsonSerializer.Serialize(recipe.ImageUrls),
            Category = recipe.Category,
            Cuisine = recipe.Cuisine,
            SuitableForDiets = recipe.SuitableForDiets,
            PrepTime = recipe.PrepTime,
            CookTime = recipe.CookTime,
            TotalTime = recipe.TotalTime,
            ServingsCount = recipe.ServingsCount,
            JsonLd = rawSchemaJson,
            CreatedAt = now,
            UpdatedAt = now,
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
