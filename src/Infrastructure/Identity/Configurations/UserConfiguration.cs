using Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Identity.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasConversion(v => v.Value, v => new UserId(v));
        builder
            .Property(e => e.Email)
            .HasConversion(v => v.Value, v => Email.Normalize(v))
            .IsRequired();
        builder
            .Property(e => e.PasswordHash)
            .HasConversion(v => v.Value, v => PasswordHash.From(v))
            .IsRequired();
        builder.Property(e => e.EmailVerified).HasDefaultValue(false);
        builder.Property(e => e.EmailVerifiedAt);
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.UpdatedAt).IsRequired();
        builder.HasIndex(e => e.Email).IsUnique();

        builder
            .HasMany(e => e.EmailVerificationTokens)
            .WithOne()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .Metadata.FindNavigation(nameof(User.EmailVerificationTokens))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder
            .HasMany(e => e.PasswordResetTokens)
            .WithOne()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .Metadata.FindNavigation(nameof(User.PasswordResetTokens))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder
            .HasMany(e => e.RecoveryCodes)
            .WithOne()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .Metadata.FindNavigation(nameof(User.RecoveryCodes))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder
            .HasMany(e => e.TwoFactorChallenges)
            .WithOne()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .Metadata.FindNavigation(nameof(User.TwoFactorChallenges))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder
            .HasOne(e => e.Totp)
            .WithOne()
            .HasForeignKey<TotpCredential>(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
