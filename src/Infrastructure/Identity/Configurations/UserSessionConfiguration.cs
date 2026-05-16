using Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Identity.Configurations;

public sealed class UserSessionConfiguration : IEntityTypeConfiguration<UserSession>
{
    public void Configure(EntityTypeBuilder<UserSession> builder)
    {
        builder.ToTable("RefreshTokens");
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
        builder.Property(e => e.RevokedAt).IsConcurrencyToken();
        builder.HasIndex(e => e.TokenHash).IsUnique();
        builder.HasIndex(e => e.UserId);
        builder
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
