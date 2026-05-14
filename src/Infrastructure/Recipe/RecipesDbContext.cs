using Domain.User;
using Infrastructure.Embedding;
using Infrastructure.User;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Pgvector.EntityFrameworkCore;
using DomainUser = Domain.User.User;

namespace Infrastructure.Recipe;

public sealed class RecipesDbContext(DbContextOptions<RecipesDbContext> options)
    : DbContext(options)
{
    public DbSet<RecipeEntity> Recipes => Set<RecipeEntity>();
    public DbSet<RecipeIngredientEntity> RecipeIngredients => Set<RecipeIngredientEntity>();
    public DbSet<RecipeInstructionEntity> RecipeInstructions => Set<RecipeInstructionEntity>();
    public DbSet<RecipeEmbeddingEntity> RecipeEmbeddings => Set<RecipeEmbeddingEntity>();
    public DbSet<DomainUser> Users => Set<DomainUser>();
    public DbSet<RecipeFavoriteEntity> RecipeFavorites => Set<RecipeFavoriteEntity>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<EmailVerificationToken> EmailVerificationTokens => Set<EmailVerificationToken>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");

        modelBuilder.Entity<RecipeEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Url).IsUnique();
            entity.Property(e => e.Url).IsRequired();
            entity.Property(e => e.Name).IsRequired();
            entity.Property(e => e.Description);
            entity.Property(e => e.ImageUrlsJson).HasColumnType("jsonb").IsRequired();
            entity.Property(e => e.JsonLd).HasColumnType("jsonb").IsRequired();
        });

        modelBuilder.Entity<RecipeIngredientEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.RecipeId).IsRequired();
            entity.Property(e => e.Text).IsRequired();
            entity.HasIndex(e => e.RecipeId);
            entity
                .HasOne(e => e.Recipe)
                .WithMany(r => r.IngredientEntities)
                .HasForeignKey(e => e.RecipeId);
        });

        modelBuilder.Entity<RecipeInstructionEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.RecipeId).IsRequired();
            entity.Property(e => e.Position).IsRequired();
            entity.Property(e => e.Text).IsRequired();
            entity.HasIndex(e => e.RecipeId);
            entity
                .HasOne(e => e.Recipe)
                .WithMany(r => r.InstructionEntities)
                .HasForeignKey(e => e.RecipeId);
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

        modelBuilder.Entity<DomainUser>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasConversion(v => v.Value, v => new UserId(v));
            entity
                .Property(e => e.Email)
                .HasConversion(v => v.Value, v => Email.Normalize(v))
                .IsRequired();
            entity
                .Property(e => e.PasswordHash)
                .HasConversion(v => v.Value, v => PasswordHash.From(v))
                .IsRequired();
            entity.Property(e => e.EmailVerified).HasDefaultValue(false);
            entity.Property(e => e.EmailVerifiedAt);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();
            entity.HasIndex(e => e.Email).IsUnique();

            entity
                .HasMany(e => e.EmailVerificationTokens)
                .WithOne()
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity
                .Metadata.FindNavigation(nameof(DomainUser.EmailVerificationTokens))!
                .SetPropertyAccessMode(PropertyAccessMode.Field);

            entity
                .HasMany(e => e.PasswordResetTokens)
                .WithOne()
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity
                .Metadata.FindNavigation(nameof(DomainUser.PasswordResetTokens))!
                .SetPropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<EmailVerificationToken>(entity =>
        {
            entity.ToTable("EmailVerificationTokens");
            entity.HasKey(e => e.Id);
            entity
                .Property(e => e.UserId)
                .HasConversion(v => v.Value, v => new UserId(v))
                .IsRequired();
            entity
                .Property(e => e.TokenHash)
                .HasConversion(v => v.Value, v => TokenHash.From(v))
                .IsRequired();
            entity.Property(e => e.ExpiresAt).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.ConsumedAt).IsConcurrencyToken();
            entity.HasIndex(e => e.TokenHash).IsUnique();
            entity.HasIndex(e => e.UserId);
        });

        modelBuilder.Entity<PasswordResetToken>(entity =>
        {
            entity.ToTable("PasswordResetTokens");
            entity.HasKey(e => e.Id);
            entity
                .Property(e => e.UserId)
                .HasConversion(v => v.Value, v => new UserId(v))
                .IsRequired();
            entity
                .Property(e => e.TokenHash)
                .HasConversion(v => v.Value, v => TokenHash.From(v))
                .IsRequired();
            entity.Property(e => e.ExpiresAt).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.ConsumedAt).IsConcurrencyToken();
            entity.HasIndex(e => e.TokenHash).IsUnique();
            entity.HasIndex(e => e.UserId);
        });

        modelBuilder.Entity<UserSession>(entity =>
        {
            entity.ToTable("RefreshTokens");
            entity.HasKey(e => e.Id);
            entity
                .Property(e => e.UserId)
                .HasConversion(v => v.Value, v => new UserId(v))
                .IsRequired();
            entity
                .Property(e => e.TokenHash)
                .HasConversion(v => v.Value, v => TokenHash.From(v))
                .IsRequired();
            entity.Property(e => e.ExpiresAt).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.RevokedAt).IsConcurrencyToken();
            entity.HasIndex(e => e.TokenHash).IsUnique();
            entity.HasIndex(e => e.UserId);
            entity
                .HasOne<DomainUser>()
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RecipeFavoriteEntity>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.RecipeId });
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.HasIndex(e => e.RecipeId);
            entity
                .HasOne<DomainUser>()
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Recipe).WithMany().HasForeignKey(e => e.RecipeId);
        });
    }
}
