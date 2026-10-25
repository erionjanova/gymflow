using GymFlow.Domain.Common;
using GymFlow.Domain.Enums;

namespace GymFlow.Domain.Entities;

public class MemberSubscription : TenantEntity
{
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public Guid PlanId { get; set; }
    public MembershipPlan? Plan { get; set; }
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Incomplete;
    public string StripeSubscriptionId { get; set; } = string.Empty;
    public string? StripeCustomerId { get; set; }
    public DateTimeOffset? CurrentPeriodEnd { get; set; }
    public bool CancelAtPeriodEnd { get; set; }
    public DateTimeOffset? CanceledAt { get; set; }
    public DateTimeOffset LastEventAt { get; set; }

    public bool GrantsAccess => Status is SubscriptionStatus.Active or SubscriptionStatus.PastDue;

    public bool Apply(
        SubscriptionStatus status,
        DateTimeOffset? currentPeriodEnd,
        bool cancelAtPeriodEnd,
        string? customerId,
        DateTimeOffset eventAt)
    {
        if (eventAt < LastEventAt)
            return false;

        if (status == SubscriptionStatus.Canceled && Status != SubscriptionStatus.Canceled)
            CanceledAt = eventAt;

        Status = status;
        CurrentPeriodEnd = currentPeriodEnd ?? CurrentPeriodEnd;
        CancelAtPeriodEnd = cancelAtPeriodEnd;
        StripeCustomerId = customerId ?? StripeCustomerId;
        LastEventAt = eventAt;
        return true;
    }
}
