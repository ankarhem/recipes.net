using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Infrastructure;

public sealed class RecipesDbContextFactory : IDesignTimeDbContextFactory<RecipesDbContext>
{
    public RecipesDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__Recipes")
            ?? "Host=localhost;Database=recipes";

        var options = new DbContextOptionsBuilder<RecipesDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new RecipesDbContext(options);
    }
}
