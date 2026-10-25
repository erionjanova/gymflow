using GymFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymFlow.Infrastructure.Persistence.Configurations;

public class InvitationConfiguration : IEntityTypeConfiguration<Invitation>
{
    public void Configure(EntityTypeBuilder<Invitation> builder)
    {
        builder.Property(i => i.Email).HasMaxLength(200).IsRequired();
        builder.Property(i => i.Role).HasConversion<string>().HasMaxLength(20);
        builder.Property(i => i.TokenHash).HasMaxLength(64).IsRequired();

        builder.HasOne(i => i.Tenant)
            .WithMany()
            .HasForeignKey(i => i.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(i => i.InvitedBy)
            .WithMany()
            .HasForeignKey(i => i.InvitedById)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(i => i.TokenHash).IsUnique();
        builder.HasIndex(i => new { i.TenantId, i.Email });
    }
}
