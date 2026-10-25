using GymFlow.Application.Billing;
using GymFlow.Application.Common.Interfaces;
using GymFlow.Domain.Enums;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;

namespace GymFlow.Infrastructure.Payments;

public class StripePaymentGateway(IOptions<StripeOptions> options) : IPaymentGateway
{
    private readonly StripeOptions _options = options.Value;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.SecretKey);

    public async Task<string> CreateCheckoutUrlAsync(CheckoutRequest request, CancellationToken ct)
    {
        try
        {
            return await CreateSessionAsync(request, ct);
        }
        catch (StripeException ex) when (request.CustomerId is not null
            && ex.StripeError?.Code == "resource_missing" && ex.StripeError.Param == "customer")
        {
            return await CreateSessionAsync(request with { CustomerId = null }, ct);
        }
    }

    private async Task<string> CreateSessionAsync(CheckoutRequest request, CancellationToken ct)
    {
        var metadata = new Dictionary<string, string>
        {
            [PaymentMetadata.TenantId] = request.TenantId.ToString(),
            [PaymentMetadata.UserId] = request.UserId.ToString(),
            [PaymentMetadata.PlanId] = request.PlanId.ToString()
        };

        var sessionOptions = new SessionCreateOptions
        {
            Mode = "subscription",
            SuccessUrl = request.SuccessUrl,
            CancelUrl = request.CancelUrl,
            Customer = request.CustomerId,
            CustomerEmail = request.CustomerId is null ? request.CustomerEmail : null,
            ClientReferenceId = request.UserId.ToString(),
            Metadata = metadata,
            SubscriptionData = new SessionSubscriptionDataOptions { Metadata = metadata },
            LineItems =
            [
                new SessionLineItemOptions
                {
                    Quantity = 1,
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = request.Currency.ToLowerInvariant(),
                        UnitAmount = (long)Math.Round(request.Price * 100, MidpointRounding.AwayFromZero),
                        Recurring = new SessionLineItemPriceDataRecurringOptions
                        {
                            Interval = request.Interval == BillingInterval.Year ? "year" : "month"
                        },
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = $"{request.GymName} – {request.PlanName}"
                        }
                    }
                }
            ]
        };

        var session = await new SessionService(CreateClient()).CreateAsync(sessionOptions, cancellationToken: ct);
        return session.Url;
    }

    public Task SetCancelAtPeriodEndAsync(string subscriptionId, bool cancel, CancellationToken ct) =>
        new SubscriptionService(CreateClient()).UpdateAsync(
            subscriptionId, new SubscriptionUpdateOptions { CancelAtPeriodEnd = cancel }, cancellationToken: ct);

    internal StripeClient CreateClient() => new(_options.SecretKey);
}
