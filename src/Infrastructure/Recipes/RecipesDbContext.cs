using Domain.Identity;
using Infrastructure.Embedding;
using Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Pgvector.EntityFrameworkCore;

namespace Infrastructure.Recipes;

public sealed class RecipesDbContext(DbContextOptions<RecipesDbContext> options)
    : DbContext(options)
{
    public DbSet<RecipeEntity> Recipes => Set<RecipeEntity>();
    public DbSet<RecipeIngredientEntity> RecipeIngredients => Set<RecipeIngredientEntity>();
    public DbSet<RecipeInstructionEntity> RecipeInstructions => Set<RecipeInstructionEntity>();
    public DbSet<RecipeEmbeddingEntity> RecipeEmbeddings => Set<RecipeEmbeddingEntity>();
    public DbSet<User> Users => Set<User>();
    public DbSet<RecipeFavoriteEntity> RecipeFavorites => Set<RecipeFavoriteEntity>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<EmailVerificationToken> EmailVerificationTokens => Set<EmailVerificationToken>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<TotpCredential> UserTotpCredentials => Set<TotpCredential>();
    public DbSet<RecoveryCode> RecoveryCodes => Set<RecoveryCode>();
    public DbSet<TwoFactorChallenge> TwoFactorChallenges => Set<TwoFactorChallenge>();

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

        modelBuilder.Entity<User>(entity =>
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
                .Metadata.FindNavigation(nameof(User.EmailVerificationTokens))!
                .SetPropertyAccessMode(PropertyAccessMode.Field);

            entity
                .HasMany(e => e.PasswordResetTokens)
                .WithOne()
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity
                .Metadata.FindNavigation(nameof(User.PasswordResetTokens))!
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
                .HasOne<User>()
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TotpCredential>(entity =>
        {
            entity.ToTable("UserTotpCredentials");
            entity.HasKey(e => e.Id);
            entity
                .Property(e => e.UserId)
                .HasConversion(v => v.Value, v => new UserId(v))
                .IsRequired();
            entity
                .Property(e => e.EncryptedSecret)
                .HasConversion(v => v.Value, v => EncryptedTotpSecret.From(v))
                .IsRequired();
            entity.Property(e => e.IsVerified).HasDefaultValue(false).IsRequired();
            entity.Property(e => e.LastUsedStep).IsConcurrencyToken();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();
            entity.Property(e => e.VerifiedAt);
            entity.HasIndex(e => e.UserId).IsUnique();
            entity
                .HasOne<User>()
                .WithOne(u => u.Totp)
                .HasForeignKey<TotpCredential>(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RecoveryCode>(entity =>
        {
            entity.ToTable("RecoveryCodes");
            entity.HasKey(e => e.Id);
            entity
                .Property(e => e.UserId)
                .HasConversion(v => v.Value, v => new UserId(v))
                .IsRequired();
            entity
                .Property(e => e.CodeHash)
                .HasConversion(v => v.Value, v => RecoveryCodeHash.From(v))
                .IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.ConsumedAt).IsConcurrencyToken();
            entity.HasIndex(e => e.UserId);
            entity
                .HasOne<User>()
                .WithMany(u => u.RecoveryCodes)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder
                .Entity<User>()
                .Metadata.FindNavigation(nameof(User.RecoveryCodes))!
                .SetPropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<TwoFactorChallenge>(entity =>
        {
            entity.ToTable("TwoFactorChallenges");
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
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.TokenHash).IsUnique();
            entity
                .HasOne<User>()
                .WithMany(u => u.TwoFactorChallenges)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder
                .Entity<User>()
                .Metadata.FindNavigation(nameof(User.TwoFactorChallenges))!
                .SetPropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<RecipeFavoriteEntity>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.RecipeId });
            entity.Property(e => e.UserId).HasConversion(v => v.Value, v => new UserId(v));
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.HasIndex(e => e.RecipeId);
            entity
                .HasOne<User>()
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Recipe).WithMany().HasForeignKey(e => e.RecipeId);
        });
    }
}
