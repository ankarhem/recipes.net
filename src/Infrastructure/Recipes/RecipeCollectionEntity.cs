using Domain.Recipes;

namespace Infrastructure.Recipes;

public sealed class RecipeCollectionEntity
{
    public Guid Id { get; set; }
    public Guid OwnerUserId { get; set; }
    public required string Name { get; set; }
    public RecipeCollectionKind Kind { get; set; }
    public RecipeCollectionVisibility Visibility { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public List<RecipeCollectionItemEntity> Items { get; set; } = [];

    public RecipeCollection ToDomain() =>
        RecipeCollection.Rehydrate(
            new RecipeCollectionId(Id),
            new RecipeCollectionOwnerId(OwnerUserId),
            Name,
            Kind,
            Visibility,
            CreatedAt,
            UpdatedAt,
            Items.OrderBy(i => i.Position)
                .Select(i => new RecipeCollectionItem
                {
                    RecipeId = new RecipeId(i.RecipeId),
                    AddedAt = i.AddedAt,
                    Position = i.Position,
                })
        );

    public static RecipeCollectionEntity FromDomain(RecipeCollection collection) =>
        new()
        {
            Id = collection.Id.Value,
            OwnerUserId = collection.OwnerId.Value,
            Name = collection.Name,
            Kind = collection.Kind,
            Visibility = collection.Visibility,
            CreatedAt = collection.CreatedAt,
            UpdatedAt = collection.UpdatedAt,
            Items = collection
                .Items.Select(item => new RecipeCollectionItemEntity
                {
                    Id = Guid.NewGuid(),
                    CollectionId = collection.Id.Value,
                    RecipeId = item.RecipeId.Value,
                    AddedAt = item.AddedAt,
                    Position = item.Position,
                })
                .ToList(),
        };

    public void UpdateFromDomain(RecipeCollection collection)
    {
        Name = collection.Name;
        Kind = collection.Kind;
        Visibility = collection.Visibility;
        UpdatedAt = collection.UpdatedAt;

        var domainRecipeIds = collection.Items.Select(item => item.RecipeId.Value).ToHashSet();
        Items.RemoveAll(item => !domainRecipeIds.Contains(item.RecipeId));

        foreach (var item in collection.Items)
        {
            var entity = Items.SingleOrDefault(existing => existing.RecipeId == item.RecipeId.Value);
            if (entity is null)
            {
                Items.Add(
                    new RecipeCollectionItemEntity
                    {
                        Id = Guid.NewGuid(),
                        CollectionId = collection.Id.Value,
                        RecipeId = item.RecipeId.Value,
                        AddedAt = item.AddedAt,
                        Position = item.Position,
                    }
                );
                continue;
            }

            entity.AddedAt = item.AddedAt;
            entity.Position = item.Position;
        }
    }
}
