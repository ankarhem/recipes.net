namespace Domain.Recipes;

public sealed class RecipeCollection
{
    public const string FavoritesName = "Favorites";

    private readonly List<RecipeCollectionItem> _items;

    private RecipeCollection(
        RecipeCollectionId id,
        RecipeCollectionOwnerId ownerId,
        string name,
        RecipeCollectionKind kind,
        RecipeCollectionVisibility visibility,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        IEnumerable<RecipeCollectionItem> items
    )
    {
        Id = id;
        OwnerId = ownerId;
        Name = NormalizeName(name);
        Kind = kind;
        Visibility = kind is RecipeCollectionKind.Favorites
            ? RecipeCollectionVisibility.Private
            : visibility;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        _items = items.OrderBy(i => i.Position).ToList();

        EnsureNoDuplicateRecipes(_items);
    }

    public RecipeCollectionId Id { get; }
    public RecipeCollectionOwnerId OwnerId { get; }
    public string Name { get; private set; }
    public RecipeCollectionKind Kind { get; }
    public RecipeCollectionVisibility Visibility { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public IReadOnlyList<RecipeCollectionItem> Items => _items;
    public bool IsDefaultFavorites => Kind is RecipeCollectionKind.Favorites;

    public static RecipeCollection CreateRegular(
        RecipeCollectionOwnerId ownerId,
        string name,
        DateTimeOffset now
    ) =>
        new(
            RecipeCollectionId.New(),
            ownerId,
            name,
            RecipeCollectionKind.Regular,
            RecipeCollectionVisibility.Private,
            now,
            now,
            []
        );

    public static RecipeCollection CreateFavorites(
        RecipeCollectionOwnerId ownerId,
        DateTimeOffset now
    ) =>
        new(
            RecipeCollectionId.New(),
            ownerId,
            FavoritesName,
            RecipeCollectionKind.Favorites,
            RecipeCollectionVisibility.Private,
            now,
            now,
            []
        );

    public static RecipeCollection Rehydrate(
        RecipeCollectionId id,
        RecipeCollectionOwnerId ownerId,
        string name,
        RecipeCollectionKind kind,
        RecipeCollectionVisibility visibility,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        IEnumerable<RecipeCollectionItem> items
    ) =>
        new(id, ownerId, name, kind, visibility, createdAt, updatedAt, items);

    public void Rename(string name, DateTimeOffset now)
    {
        if (IsDefaultFavorites)
        {
            throw new InvalidOperationException("The default Favorites collection cannot be renamed.");
        }

        Name = NormalizeName(name);
        Touch(now);
    }

    public void Share(DateTimeOffset now)
    {
        if (IsDefaultFavorites)
        {
            throw new InvalidOperationException("The default Favorites collection cannot be shared.");
        }

        Visibility = RecipeCollectionVisibility.Shared;
        Touch(now);
    }

    public void MakePrivate(DateTimeOffset now)
    {
        Visibility = RecipeCollectionVisibility.Private;
        Touch(now);
    }

    public bool AddRecipe(RecipeId recipeId, DateTimeOffset now)
    {
        if (_items.Any(i => i.RecipeId == recipeId))
        {
            return false;
        }

        _items.Add(
            new RecipeCollectionItem
            {
                RecipeId = recipeId,
                AddedAt = now,
                Position = _items.Count + 1,
            }
        );
        Touch(now);
        return true;
    }

    public bool RemoveRecipe(RecipeId recipeId, DateTimeOffset now)
    {
        var item = _items.SingleOrDefault(i => i.RecipeId == recipeId);
        if (item is null)
        {
            return false;
        }

        _items.Remove(item);
        RepositionItems();
        Touch(now);
        return true;
    }

    public bool ToggleRecipe(RecipeId recipeId, DateTimeOffset now)
    {
        if (ContainsRecipe(recipeId))
        {
            RemoveRecipe(recipeId, now);
            return false;
        }

        AddRecipe(recipeId, now);
        return true;
    }

    public bool ContainsRecipe(RecipeId recipeId) => _items.Any(i => i.RecipeId == recipeId);

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Collection name must not be empty.", nameof(name));
        }

        return name.Trim();
    }

    private static void EnsureNoDuplicateRecipes(IEnumerable<RecipeCollectionItem> items)
    {
        var duplicate = items.GroupBy(i => i.RecipeId).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException(
                $"Collection contains duplicate recipe '{duplicate.Key}'."
            );
        }
    }

    private void RepositionItems()
    {
        for (var index = 0; index < _items.Count; index++)
        {
            _items[index] = _items[index] with { Position = index + 1 };
        }
    }

    private void Touch(DateTimeOffset now) => UpdatedAt = now;
}
