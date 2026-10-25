using GymFlow.Domain.Enums;

namespace GymFlow.Application.Billing;

public record CheckoutDto(string Url);

public record MySubscriptionDto(
    Guid Id,
    Guid PlanId,
    string PlanName,
    decimal Price,
    BillingInterval Interval,
    SubscriptionStatus Status,
    DateTimeOffset? CurrentPeriodEnd,
    bool CancelAtPeriodEnd);

public record GymSubscriptionDto(
    Guid Id,
    Guid UserId,
    string MemberName,
    string MemberEmail,
    string PlanName,
    decimal Price,
    BillingInterval Interval,
    SubscriptionStatus Status,
    DateTimeOffset? CurrentPeriodEnd,
    bool CancelAtPeriodEnd,
    DateTimeOffset CreatedAt);

public record BillingOverviewDto(
    int ActiveMembers,
    decimal MonthlyRecurringRevenue,
    string Currency,
    IReadOnlyList<GymSubscriptionDto> Subscriptions);

public record SetCancelRequest(bool CancelAtPeriodEnd);

public record SyncCheckoutRequest(string CheckoutSessionId);

public record ChangePlanRequest(Guid PlanId);

public record SubscriptionUpdate(
    string StripeSubscriptionId,
    string StripeStatus,
    string? CustomerId,
    DateTimeOffset? CurrentPeriodEnd,
    bool CancelAtPeriodEnd,
    DateTimeOffset EventAt,
    Guid? TenantId,
    Guid? UserId,
    Guid? PlanId);

public static class PaymentMetadata
{
    public const string TenantId = "tenant_id";
    public const string UserId = "user_id";
    public const string PlanId = "plan_id";
}
