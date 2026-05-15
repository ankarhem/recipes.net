namespace Domain.Recipes;

public readonly record struct RecipeId(Guid Value)
{
    public static RecipeId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
