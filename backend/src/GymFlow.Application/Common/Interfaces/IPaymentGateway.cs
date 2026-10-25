using GymFlow.Application.Billing;
using GymFlow.Domain.Enums;

namespace GymFlow.Application.Common.Interfaces;

public record CheckoutRequest(
    Guid TenantId,
    Guid UserId,
    Guid PlanId,
    string GymName,
    string PlanName,
    decimal Price,
    string Currency,
    BillingInterval Interval,
    string CustomerEmail,
    string? CustomerId,
    string SuccessUrl,
    string CancelUrl);

public record PlanChangeRequest(
    string SubscriptionId,
    Guid TenantId,
    Guid UserId,
    Guid PlanId,
    string GymName,
    string PlanName,
    decimal Price,
    string Currency,
    BillingInterval Interval);

public interface IPaymentGateway
{
    bool IsConfigured { get; }

    Task<string> CreateCheckoutUrlAsync(CheckoutRequest request, CancellationToken ct);

    Task SetCancelAtPeriodEndAsync(string subscriptionId, bool cancel, CancellationToken ct);

    Task<SubscriptionUpdate?> GetCheckoutSubscriptionAsync(string checkoutSessionId, CancellationToken ct);

    Task<SubscriptionUpdate> ChangePlanAsync(PlanChangeRequest request, CancellationToken ct);
}
