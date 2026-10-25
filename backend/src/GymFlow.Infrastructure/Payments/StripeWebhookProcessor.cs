using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;
using BillingService = GymFlow.Application.Billing.BillingService;

namespace GymFlow.Infrastructure.Payments;

public class StripeWebhookProcessor(
    IOptions<StripeOptions> options,
    StripePaymentGateway gateway,
    BillingService billing,
    ILogger<StripeWebhookProcessor> logger)
{
    public async Task ProcessAsync(string json, string signature, CancellationToken ct)
    {
        var stripeEvent = EventUtility.ConstructEvent(
            json, signature, options.Value.WebhookSecret, throwOnApiVersionMismatch: false);
        var eventAt = new DateTimeOffset(DateTime.SpecifyKind(stripeEvent.Created, DateTimeKind.Utc));

        switch (stripeEvent.Type)
        {
            case EventTypes.CustomerSubscriptionCreated:
            case EventTypes.CustomerSubscriptionUpdated:
            case EventTypes.CustomerSubscriptionDeleted:
                await billing.ApplyUpdateAsync(StripePaymentGateway.ToUpdate((Subscription)stripeEvent.Data.Object, eventAt), ct);
                break;

            case EventTypes.CheckoutSessionCompleted:
                var session = (Session)stripeEvent.Data.Object;
                if (session.SubscriptionId is null)
                    break;

                var subscription = await new SubscriptionService(gateway.CreateClient())
                    .GetAsync(session.SubscriptionId, cancellationToken: ct);
                await billing.ApplyUpdateAsync(StripePaymentGateway.ToUpdate(subscription, eventAt), ct);
                break;

            default:
                logger.LogDebug("Ignoring Stripe event {Type}", stripeEvent.Type);
                break;
        }
    }
}
