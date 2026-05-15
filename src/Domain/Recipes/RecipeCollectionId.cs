namespace Domain.Recipes;

public readonly record struct RecipeCollectionId(Guid Value)
{
    public static RecipeCollectionId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
