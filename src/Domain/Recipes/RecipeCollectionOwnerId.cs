namespace Domain.Recipes;

public readonly record struct RecipeCollectionOwnerId(Guid Value)
{
    public override string ToString() => Value.ToString();
}
