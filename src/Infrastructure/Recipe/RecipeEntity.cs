using DomainRecipe = Domain.Recipe.Recipe;

namespace Infrastructure.Recipe;

public sealed class RecipeEntity
{
    public Guid Id { get; set; }
    public required string Url { get; set; }
    public required string Name { get; set; }
    public required string JsonLd { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public static RecipeEntity FromImport(
        DomainRecipe recipe,
        string sourceUrl,
        string rawSchemaJson
    )
    {
        return new RecipeEntity
        {
            Id = Guid.NewGuid(),
            Url = sourceUrl,
            Name = recipe.Name ?? "Untitled",
            JsonLd = rawSchemaJson,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
    }
}
