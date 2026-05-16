using Domain.Identity;
using Domain.Recipes;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;

namespace Infrastructure.Recipes;

public sealed class RecipesDbContext(DbContextOptions<RecipesDbContext> options)
    : DbContext(options)
{
    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<RecipeCollection> RecipeCollections => Set<RecipeCollection>();
    public DbSet<RecipeEmbedding> RecipeEmbeddings => Set<RecipeEmbedding>();
    public DbSet<User> Users => Set<User>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<EmailVerificationToken> EmailVerificationTokens => Set<EmailVerificationToken>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<TotpCredential> UserTotpCredentials => Set<TotpCredential>();
    public DbSet<RecoveryCode> RecoveryCodes => Set<RecoveryCode>();
    public DbSet<TwoFactorChallenge> TwoFactorChallenges => Set<TwoFactorChallenge>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RecipesDbContext).Assembly);
    }
}
