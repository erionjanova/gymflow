using GymFlow.Application.Billing;
using GymFlow.Application.Common.Interfaces;
using GymFlow.Domain.Enums;

namespace GymFlow.Infrastructure.Payments;

public class DemoPaymentGateway(TimeProvider timeProvider) : IPaymentGateway
{
    public const string SessionPrefix = "cs_demo_";

    public bool IsConfigured => true;

    public Task<string> CreateCheckoutUrlAsync(CheckoutRequest request, CancellationToken ct)
    {
        var interval = request.Interval == BillingInterval.Year ? "y" : "m";
        var sessionId = $"{SessionPrefix}{request.TenantId:N}_{request.UserId:N}_{request.PlanId:N}_{interval}";
        return Task.FromResult(request.SuccessUrl.Replace("{CHECKOUT_SESSION_ID}", sessionId));
    }

    public Task SetCancelAtPeriodEndAsync(string subscriptionId, bool cancel, CancellationToken ct) => Task.CompletedTask;

    public Task<SubscriptionUpdate?> GetCheckoutSubscriptionAsync(string checkoutSessionId, CancellationToken ct)
    {
        var parts = checkoutSessionId.StartsWith(SessionPrefix)
            ? checkoutSessionId[SessionPrefix.Length..].Split('_')
            : [];

        if (parts.Length != 4
            || !Guid.TryParse(parts[0], out var tenantId)
            || !Guid.TryParse(parts[1], out var userId)
            || !Guid.TryParse(parts[2], out var planId))
            return Task.FromResult<SubscriptionUpdate?>(null);

        var interval = parts[3] == "y" ? BillingInterval.Year : BillingInterval.Month;
        return Task.FromResult<SubscriptionUpdate?>(
            Active($"sub_demo_{tenantId:N}_{userId:N}", tenantId, userId, planId, interval));
    }

    public Task<SubscriptionUpdate> ChangePlanAsync(PlanChangeRequest request, CancellationToken ct) =>
        Task.FromResult(Active(request.SubscriptionId, request.TenantId, request.UserId, request.PlanId, request.Interval));

    private SubscriptionUpdate Active(string subscriptionId, Guid tenantId, Guid userId, Guid planId, BillingInterval interval)
    {
        var now = timeProvider.GetUtcNow();
        var periodEnd = interval == BillingInterval.Year ? now.AddYears(1) : now.AddMonths(1);
        return new SubscriptionUpdate(subscriptionId, "active", null, periodEnd, false, now, tenantId, userId, planId);
    }
}
