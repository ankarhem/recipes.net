using Domain.Recipes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Recipes.Configurations;

public sealed class RecipeCollectionConfiguration : IEntityTypeConfiguration<RecipeCollection>
{
    public void Configure(EntityTypeBuilder<RecipeCollection> builder)
    {
        // TODO: RecipeCollection needs a private parameterless constructor before this direct domain mapping is applied.
        builder.UsePropertyAccessMode(PropertyAccessMode.PreferFieldDuringConstruction);

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasConversion(v => v.Value, v => new RecipeCollectionId(v));
        builder
            .Property(e => e.OwnerId)
            .HasConversion(v => v.Value, v => new RecipeCollectionOwnerId(v))
            .IsRequired();
        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Kind).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(e => e.Visibility).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.UpdatedAt).IsRequired();
        builder.HasIndex(e => e.OwnerId);
        builder
            .HasIndex(e => new { e.OwnerId, e.Kind })
            .IsUnique()
            .HasFilter("\"Kind\" = 'Favorites'");

        builder.OwnsMany(
            e => e.Items,
            owned =>
            {
                owned.ToTable("RecipeCollectionItems");
                owned.Property<Guid>("Id").ValueGeneratedOnAdd();
                owned.HasKey("Id");
                owned.WithOwner().HasForeignKey("CollectionId");
                owned
                    .Property(i => i.RecipeId)
                    .HasConversion(v => v.Value, v => new RecipeId(v))
                    .IsRequired();
                owned.Property(i => i.AddedAt).IsRequired();
                owned.Property(i => i.Position).IsRequired();
                owned.HasIndex("CollectionId", nameof(RecipeCollectionItem.RecipeId)).IsUnique();
                owned.HasIndex(i => i.RecipeId);
                owned.HasOne<Recipe>().WithMany().HasForeignKey(i => i.RecipeId);
            }
        );
        builder
            .Metadata.FindNavigation(nameof(RecipeCollection.Items))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
