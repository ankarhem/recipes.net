using Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Identity.Configurations;

public sealed class TotpCredentialConfiguration : IEntityTypeConfiguration<TotpCredential>
{
    public void Configure(EntityTypeBuilder<TotpCredential> builder)
    {
        builder.ToTable("UserTotpCredentials");
        builder.HasKey(e => e.Id);
        builder
            .Property(e => e.UserId)
            .HasConversion(v => v.Value, v => new UserId(v))
            .IsRequired();
        builder
            .Property(e => e.EncryptedSecret)
            .HasConversion(v => v.Value, v => EncryptedTotpSecret.From(v))
            .IsRequired();
        builder.Property(e => e.IsVerified).HasDefaultValue(false).IsRequired();
        builder.Property(e => e.LastUsedStep).IsConcurrencyToken();
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.UpdatedAt).IsRequired();
        builder.Property(e => e.VerifiedAt);
        builder.HasIndex(e => e.UserId).IsUnique();
        builder
            .HasOne<User>()
            .WithOne(u => u.Totp)
            .HasForeignKey<TotpCredential>(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
