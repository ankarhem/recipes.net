using Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Identity.Configurations;

public sealed class RecoveryCodeConfiguration : IEntityTypeConfiguration<RecoveryCode>
{
    public void Configure(EntityTypeBuilder<RecoveryCode> builder)
    {
        builder.ToTable("RecoveryCodes");
        builder.HasKey(e => e.Id);
        builder
            .Property(e => e.UserId)
            .HasConversion(v => v.Value, v => new UserId(v))
            .IsRequired();
        builder
            .Property(e => e.CodeHash)
            .HasConversion(v => v.Value, v => RecoveryCodeHash.From(v))
            .IsRequired();
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.ConsumedAt).IsConcurrencyToken();
        builder.HasIndex(e => e.UserId);
        builder
            .HasOne<User>()
            .WithMany(u => u.RecoveryCodes)
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
