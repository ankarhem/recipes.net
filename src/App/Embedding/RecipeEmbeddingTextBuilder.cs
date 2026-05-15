using Domain.Recipes;
using System.Security.Cryptography;
using System.Text;

namespace App.Embedding;

public sealed class RecipeEmbeddingTextBuilder : IRecipeEmbeddingTextBuilder
{
    public string Build(Recipe recipe)
    {
        var sb = new StringBuilder();

        if (recipe.Name is not null)
        {
            sb.AppendLine($"Name: {recipe.Name}");
        }

        if (recipe.Description is not null)
        {
            sb.AppendLine($"Description: {recipe.Description}");
        }

        if (recipe.Ingredients.Count > 0)
        {
            sb.AppendLine("Ingredients:");
            foreach (var ingredient in recipe.Ingredients)
            {
                sb.AppendLine($"- {ingredient.Text}");
            }
        }

        if (recipe.Instructions.Count > 0)
        {
            sb.AppendLine("Instructions:");
            foreach (var instruction in recipe.Instructions.OrderBy(i => i.Position))
            {
                sb.AppendLine($"{instruction.Position}. {instruction.Text}");
            }
        }

        return sb.ToString().TrimEnd();
    }

    public static string ComputeInputHash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
