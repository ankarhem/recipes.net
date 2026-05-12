namespace Domain.Recipe;

public sealed record RecipeInstruction(int Position, string Text, string? Name = null);
