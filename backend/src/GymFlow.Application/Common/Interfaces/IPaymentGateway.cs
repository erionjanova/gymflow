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

public interface IPaymentGateway
{
    bool IsConfigured { get; }

    Task<string> CreateCheckoutUrlAsync(CheckoutRequest request, CancellationToken ct);

    Task SetCancelAtPeriodEndAsync(string subscriptionId, bool cancel, CancellationToken ct);
}
