using Infrastructure.Embedding;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;

namespace Infrastructure.Recipe;

public sealed class RecipesDbContext(DbContextOptions<RecipesDbContext> options)
    : DbContext(options)
{
    public DbSet<RecipeEntity> Recipes => Set<RecipeEntity>();
    public DbSet<RecipeEmbeddingEntity> RecipeEmbeddings => Set<RecipeEmbeddingEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");

        modelBuilder.Entity<RecipeEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Url).IsUnique();
            entity.Property(e => e.Url).IsRequired();
            entity.Property(e => e.Name).IsRequired();
            entity.Property(e => e.JsonLd).HasColumnType("jsonb").IsRequired();
        });

        modelBuilder.Entity<RecipeEmbeddingEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.RecipeId).IsRequired();
            entity.Property(e => e.Model).IsRequired();
            entity.Property(e => e.Dimensions).IsRequired();
            entity.Property(e => e.InputHash).IsRequired();
            entity.Property(e => e.Embedding).HasColumnType("vector").IsRequired();
            entity.HasIndex(e => e.RecipeId);
            entity
                .HasIndex(e => new
                {
                    e.RecipeId,
                    e.Model,
                    e.Dimensions,
                    e.InputHash,
                })
                .IsUnique();
            entity.HasOne(e => e.Recipe).WithMany().HasForeignKey(e => e.RecipeId);
        });
    }
}
