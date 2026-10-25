using GymFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymFlow.Infrastructure.Persistence.Configurations;

public static class ClassSessionConstraints
{
    public const string Counts = "ck_class_sessions_counts";
}

public class ClassSessionConfiguration : IEntityTypeConfiguration<ClassSession>
{
    public void Configure(EntityTypeBuilder<ClassSession> builder)
    {
        builder.ToTable(t => t.HasCheckConstraint(
            ClassSessionConstraints.Counts,
            "booked_count >= 0 AND booked_count <= capacity AND waitlist_count >= 0 AND ends_at > starts_at"));

        builder.Property(s => s.Version).IsRowVersion();
        builder.Ignore(s => s.SpotsLeft);

        builder.HasOne(s => s.Tenant)
            .WithMany()
            .HasForeignKey(s => s.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.GymClass)
            .WithMany(c => c.Sessions)
            .HasForeignKey(s => s.GymClassId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.Trainer)
            .WithMany()
            .HasForeignKey(s => s.TrainerId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(s => new { s.TenantId, s.StartsAt });
        builder.HasIndex(s => new { s.GymClassId, s.StartsAt });
        builder.HasIndex(s => new { s.TrainerId, s.StartsAt });
    }
}
