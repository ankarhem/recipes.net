namespace Domain;

public sealed class RecipeEntity
{
    public Guid Id { get; set; }
    public required string Url { get; set; }
    public required string Name { get; set; }
    public required string JsonLd { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
