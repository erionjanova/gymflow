using GymFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymFlow.Infrastructure.Persistence.Configurations;

public static class BookingConstraints
{
    public const string OneActivePerUser = "ix_bookings_session_id_user_id";
}

public class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.Property(b => b.Status).HasConversion<string>().HasMaxLength(20);
        builder.Ignore(b => b.IsActive);

        builder.HasOne(b => b.Tenant)
            .WithMany()
            .HasForeignKey(b => b.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(b => b.Session)
            .WithMany(s => s.Bookings)
            .HasForeignKey(b => b.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(b => b.User)
            .WithMany()
            .HasForeignKey(b => b.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(b => new { b.SessionId, b.UserId })
            .HasDatabaseName(BookingConstraints.OneActivePerUser)
            .IsUnique()
            .HasFilter("status IN ('Booked', 'Waitlisted')");

        builder.HasIndex(b => new { b.SessionId, b.Status, b.CreatedAt });
        builder.HasIndex(b => new { b.TenantId, b.UserId });
    }
}
