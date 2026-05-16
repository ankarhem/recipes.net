using Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Identity.Configurations;

public sealed class TwoFactorChallengeConfiguration : IEntityTypeConfiguration<TwoFactorChallenge>
{
    public void Configure(EntityTypeBuilder<TwoFactorChallenge> builder)
    {
        builder.ToTable("TwoFactorChallenges");
        builder.HasKey(e => e.Id);
        builder
            .Property(e => e.UserId)
            .HasConversion(v => v.Value, v => new UserId(v))
            .IsRequired();
        builder
            .Property(e => e.TokenHash)
            .HasConversion(v => v.Value, v => TokenHash.From(v))
            .IsRequired();
        builder.Property(e => e.ExpiresAt).IsRequired();
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.ConsumedAt).IsConcurrencyToken();
        builder.HasIndex(e => e.UserId);
        builder.HasIndex(e => e.TokenHash).IsUnique();
        builder
            .HasOne<User>()
            .WithMany(u => u.TwoFactorChallenges)
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
