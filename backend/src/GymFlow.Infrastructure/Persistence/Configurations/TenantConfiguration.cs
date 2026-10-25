using GymFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymFlow.Infrastructure.Persistence.Configurations;

public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.Property(t => t.Name).HasMaxLength(100).IsRequired();
        builder.Property(t => t.Slug).HasMaxLength(40).IsRequired();
        builder.Property(t => t.Description).HasMaxLength(1000);
        builder.Property(t => t.Address).HasMaxLength(200);
        builder.Property(t => t.Phone).HasMaxLength(20);
        builder.Property(t => t.BrandColor).HasMaxLength(7).IsRequired();
        builder.Property(t => t.Currency).HasMaxLength(3).IsRequired();
        builder.Property(t => t.TimeZone).HasMaxLength(64).IsRequired();

        builder.HasIndex(t => t.Slug).IsUnique();
    }
}
