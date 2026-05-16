using System.Text.Json;
using Domain.Recipes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Recipes.Configurations;

public sealed class RecipeConfiguration : IEntityTypeConfiguration<Recipe>
{
    public void Configure(EntityTypeBuilder<Recipe> builder)
    {
        // TODO: Recipe needs a private parameterless constructor before this direct domain mapping is applied.
        builder.UsePropertyAccessMode(PropertyAccessMode.PreferFieldDuringConstruction);

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasConversion(v => v.Value, v => new RecipeId(v));

        builder.Property<string>("Url").IsRequired();
        builder.HasIndex("Url").IsUnique();
        builder.Property<string>("JsonLd").HasColumnType("jsonb").IsRequired();
        builder.Property<DateTimeOffset>("CreatedAt").IsRequired();
        builder.Property<DateTimeOffset>("UpdatedAt").IsRequired();

        builder.Property(e => e.Name).IsRequired();
        builder.Property(e => e.Description);
        builder
            .Property(e => e.ImageUrls)
            .HasConversion(
                v => SerializeImageUrls(v),
                v => DeserializeImageUrls(v),
                new ValueComparer<IReadOnlyList<string>>(
                    (a, b) => a!.SequenceEqual(b!),
                    v => v.Aggregate(0, (acc, url) => HashCode.Combine(acc, url)),
                    v => v.ToList().AsReadOnly()
                )
            )
            .HasColumnType("jsonb")
            .IsRequired();
        builder.Property(e => e.Category).HasMaxLength(200);
        builder.Property(e => e.Cuisine).HasMaxLength(200);
        builder
            .Property(e => e.SuitableForDiets)
            .HasConversion(
                v => v.Select(d => d.ToString()).ToArray(),
                v => v.Select(s => Enum.Parse<DietType>(s)).ToList().AsReadOnly(),
                new ValueComparer<IReadOnlyList<DietType>>(
                    (a, b) => a!.OrderBy(d => d).SequenceEqual(b!.OrderBy(d => d)),
                    v => v.OrderBy(d => d).Aggregate(0, (acc, d) => HashCode.Combine(acc, d)),
                    v => v.ToList().AsReadOnly()
                )
            )
            .HasColumnType("text[]")
            .HasDefaultValueSql("ARRAY[]::text[]");
        builder.Property(e => e.PrepTime);
        builder.Property(e => e.CookTime);
        builder.Property(e => e.TotalTime);
        builder.Property(e => e.ServingsCount);

        builder.OwnsMany(
            e => e.Ingredients,
            owned =>
            {
                owned.ToTable("RecipeIngredients");
                owned.Property<Guid>("Id").ValueGeneratedOnAdd();
                owned.HasKey("Id");
                owned.WithOwner().HasForeignKey("RecipeId");
                owned.Property(i => i.Text).IsRequired();
            }
        );
        builder
            .Metadata.FindNavigation(nameof(Recipe.Ingredients))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(
            e => e.Instructions,
            owned =>
            {
                owned.ToTable("RecipeInstructions");
                owned.Property<Guid>("Id").ValueGeneratedOnAdd();
                owned.HasKey("Id");
                owned.WithOwner().HasForeignKey("RecipeId");
                owned.Property(i => i.Position).IsRequired();
                owned.Property(i => i.Text).IsRequired();
                owned.Property(i => i.Name);
            }
        );
        builder
            .Metadata.FindNavigation(nameof(Recipe.Instructions))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }

    private static string SerializeImageUrls(IReadOnlyList<string> imageUrls) =>
        JsonSerializer.Serialize(imageUrls);

    private static IReadOnlyList<string> DeserializeImageUrls(string json) =>
        (JsonSerializer.Deserialize<List<string>>(json) ?? []).AsReadOnly();
}
