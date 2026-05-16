using Domain.Recipes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pgvector;

namespace Infrastructure.Recipes.Configurations;

public sealed class RecipeEmbeddingConfiguration : IEntityTypeConfiguration<RecipeEmbedding>
{
    public void Configure(EntityTypeBuilder<RecipeEmbedding> builder)
    {
        builder.ToTable("RecipeEmbeddings");
        builder.HasKey(e => e.Id);
        builder
            .Property(e => e.RecipeId)
            .HasConversion(v => v.Value, v => new RecipeId(v))
            .IsRequired();
        builder.Property(e => e.Model).IsRequired();
        builder.Property(e => e.Dimensions).IsRequired();
        builder.Property(e => e.InputHash).IsRequired();
        builder
            .Property(e => e.Embedding)
            .HasConversion(
                v => new Vector(v),
                v => v.ToArray()
            )
            .HasColumnType("vector")
            .IsRequired();
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.UpdatedAt).IsRequired();
        builder.HasIndex(e => e.RecipeId);
        builder
            .HasIndex(e => new
            {
                e.RecipeId,
                e.Model,
                e.Dimensions,
                e.InputHash,
            })
            .IsUnique();
        builder.HasOne<Recipe>().WithMany().HasForeignKey(e => e.RecipeId);
    }
}
