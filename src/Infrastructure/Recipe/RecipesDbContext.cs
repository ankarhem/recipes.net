using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Recipe;

public sealed class RecipesDbContext(DbContextOptions<RecipesDbContext> options)
    : DbContext(options)
{
    public DbSet<RecipeEntity> Recipes => Set<RecipeEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RecipeEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Url).IsUnique();
            entity.Property(e => e.Url).IsRequired();
            entity.Property(e => e.Name).IsRequired();
            entity.Property(e => e.JsonLd).HasColumnType("jsonb").IsRequired();
        });
    }
}
