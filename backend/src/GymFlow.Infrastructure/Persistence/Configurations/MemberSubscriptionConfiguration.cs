using GymFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GymFlow.Infrastructure.Persistence.Configurations;

public static class MemberSubscriptionConstraints
{
    public const string UniqueStripeId = "ix_member_subscriptions_stripe_subscription_id";
}

public class MemberSubscriptionConfiguration : IEntityTypeConfiguration<MemberSubscription>
{
    public void Configure(EntityTypeBuilder<MemberSubscription> builder)
    {
        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(s => s.StripeSubscriptionId).HasMaxLength(255).IsRequired();
        builder.Property(s => s.StripeCustomerId).HasMaxLength(255);
        builder.Ignore(s => s.GrantsAccess);

        builder.HasOne(s => s.Tenant)
            .WithMany()
            .HasForeignKey(s => s.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.User)
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.Plan)
            .WithMany()
            .HasForeignKey(s => s.PlanId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => s.StripeSubscriptionId).IsUnique();
        builder.HasIndex(s => new { s.TenantId, s.UserId, s.Status });
    }
}
