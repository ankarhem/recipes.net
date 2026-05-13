using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Pgvector.EntityFrameworkCore;

namespace Infrastructure.Recipe;

public sealed class RecipesDbContextFactory : IDesignTimeDbContextFactory<RecipesDbContext>
{
    public RecipesDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__Recipes")
            ?? "Host=localhost;Database=recipes";

        var options = new DbContextOptionsBuilder<RecipesDbContext>()
            .UseNpgsql(connectionString, o => o.UseVector())
            .Options;

        return new RecipesDbContext(options);
    }
}
