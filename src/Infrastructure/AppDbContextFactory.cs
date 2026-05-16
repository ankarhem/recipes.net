using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Pgvector.EntityFrameworkCore;

namespace Infrastructure;

public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__Recipes")
            ?? "Host=localhost;Database=recipes";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString, o => o.UseVector())
            .Options;

        return new AppDbContext(options);
    }
}
