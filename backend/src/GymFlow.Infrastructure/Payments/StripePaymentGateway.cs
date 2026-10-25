using GymFlow.Application.Billing;
using GymFlow.Application.Common.Interfaces;
using GymFlow.Domain.Enums;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;

namespace GymFlow.Infrastructure.Payments;

public class StripePaymentGateway(IOptions<StripeOptions> options, TimeProvider timeProvider) : IPaymentGateway
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
                        UnitAmount = ToMinorUnits(request.Price),
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

    public async Task<SubscriptionUpdate?> GetCheckoutSubscriptionAsync(string checkoutSessionId, CancellationToken ct)
    {
        var client = CreateClient();
        var session = await new SessionService(client).GetAsync(checkoutSessionId, cancellationToken: ct);
        if (session.Status != "complete" || session.SubscriptionId is null)
            return null;

        var subscription = await new SubscriptionService(client).GetAsync(session.SubscriptionId, cancellationToken: ct);
        return ToUpdate(subscription, timeProvider.GetUtcNow());
    }

    public async Task<SubscriptionUpdate> ChangePlanAsync(PlanChangeRequest request, CancellationToken ct)
    {
        var client = CreateClient();
        var subscriptions = new SubscriptionService(client);
        var current = await subscriptions.GetAsync(request.SubscriptionId, cancellationToken: ct);

        var price = await new PriceService(client).CreateAsync(new PriceCreateOptions
        {
            Currency = request.Currency.ToLowerInvariant(),
            UnitAmount = ToMinorUnits(request.Price),
            Recurring = new PriceRecurringOptions { Interval = request.Interval == BillingInterval.Year ? "year" : "month" },
            ProductData = new PriceProductDataOptions { Name = $"{request.GymName} – {request.PlanName}" }
        }, cancellationToken: ct);

        var updated = await subscriptions.UpdateAsync(request.SubscriptionId, new SubscriptionUpdateOptions
        {
            Items = [new SubscriptionItemOptions { Id = current.Items.Data[0].Id, Price = price.Id }],
            CancelAtPeriodEnd = false,
            ProrationBehavior = "create_prorations",
            Metadata = new Dictionary<string, string>
            {
                [PaymentMetadata.TenantId] = request.TenantId.ToString(),
                [PaymentMetadata.UserId] = request.UserId.ToString(),
                [PaymentMetadata.PlanId] = request.PlanId.ToString()
            }
        }, cancellationToken: ct);

        return ToUpdate(updated, timeProvider.GetUtcNow());
    }

    internal StripeClient CreateClient() => new(_options.SecretKey);

    private static long ToMinorUnits(decimal amount) => (long)Math.Round(amount * 100, MidpointRounding.AwayFromZero);

    internal static SubscriptionUpdate ToUpdate(Subscription s, DateTimeOffset eventAt)
    {
        var periodEnd = s.Items?.Data?.Select(i => i.CurrentPeriodEnd).DefaultIfEmpty().Max();

        return new SubscriptionUpdate(
            s.Id,
            s.Status,
            s.CustomerId,
            periodEnd is { } end && end != default ? new DateTimeOffset(DateTime.SpecifyKind(end, DateTimeKind.Utc)) : null,
            s.CancelAtPeriodEnd,
            eventAt,
            ReadGuid(s.Metadata, PaymentMetadata.TenantId),
            ReadGuid(s.Metadata, PaymentMetadata.UserId),
            ReadGuid(s.Metadata, PaymentMetadata.PlanId));
    }

    private static Guid? ReadGuid(IDictionary<string, string>? metadata, string key) =>
        metadata is not null && metadata.TryGetValue(key, out var value) && Guid.TryParse(value, out var id) ? id : null;
}
