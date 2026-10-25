using GymFlow.Application.Common;
using GymFlow.Application.Common.Exceptions;
using GymFlow.Application.Common.Interfaces;
using GymFlow.Application.Tenancy;
using GymFlow.Domain.Entities;
using GymFlow.Domain.Enums;
using GymFlow.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GymFlow.Application.Billing;

public class BillingService(
    IApplicationDbContext db,
    ITenantContext tenantContext,
    ICurrentUser currentUser,
    IPaymentGateway gateway,
    IOptions<AppOptions> appOptions,
    ILogger<BillingService> logger)
{
    public async Task<CheckoutDto> StartCheckoutAsync(Guid planId, CancellationToken ct)
    {
        if (!gateway.IsConfigured)
            throw new DomainException("payments_not_configured", "Online payments are not set up yet.");

        var userId = currentUser.RequireUserId();
        var tenantId = tenantContext.RequireTenantId();

        var plan = await db.MembershipPlans.AsNoTracking().SingleOrDefaultAsync(p => p.Id == planId && p.IsActive, ct)
            ?? throw new NotFoundException("plan_not_found", "Membership plan not found.");

        if (await db.MemberSubscriptions.AnyAsync(s => s.UserId == userId
                && (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.PastDue), ct))
            throw new ConflictException("already_subscribed", "You already have an active membership at this gym.");

        var gym = await db.Tenants.AsNoTracking().SingleAsync(t => t.Id == tenantId, ct);
        var email = await db.Users.Where(u => u.Id == userId).Select(u => u.Email).SingleAsync(ct);
        var customerId = await db.MemberSubscriptions
            .Where(s => s.UserId == userId && s.StripeCustomerId != null)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => s.StripeCustomerId)
            .FirstOrDefaultAsync(ct);

        var baseUrl = $"{appOptions.Value.FrontendUrl.TrimEnd('/')}/g/{gym.Slug}/membership";
        var url = await gateway.CreateCheckoutUrlAsync(new CheckoutRequest(
            tenantId, userId, plan.Id, gym.Name, plan.Name, plan.Price, gym.Currency, plan.Interval,
            email, customerId, $"{baseUrl}?checkout=success", $"{baseUrl}?checkout=cancelled"), ct);

        return new CheckoutDto(url);
    }

    public async Task<MySubscriptionDto?> GetMineAsync(CancellationToken ct)
    {
        var userId = currentUser.RequireUserId();

        return await db.MemberSubscriptions.AsNoTracking()
            .Where(s => s.UserId == userId && s.Status != SubscriptionStatus.Incomplete)
            .OrderByDescending(s => s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.PastDue)
            .ThenByDescending(s => s.CreatedAt)
            .Select(s => new MySubscriptionDto(
                s.Id, s.PlanId, s.Plan!.Name, s.Plan.Price, s.Plan.Interval, s.Status, s.CurrentPeriodEnd, s.CancelAtPeriodEnd))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<MySubscriptionDto> SetCancelAtPeriodEndAsync(bool cancel, CancellationToken ct)
    {
        var userId = currentUser.RequireUserId();

        var subscription = await db.MemberSubscriptions
            .SingleOrDefaultAsync(s => s.UserId == userId
                && (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.PastDue), ct)
            ?? throw new NotFoundException("subscription_not_found", "You don't have an active membership.");

        await gateway.SetCancelAtPeriodEndAsync(subscription.StripeSubscriptionId, cancel, ct);
        subscription.CancelAtPeriodEnd = cancel;
        await db.SaveChangesAsync(ct);

        return (await GetMineAsync(ct))!;
    }

    public async Task<BillingOverviewDto> GetOverviewAsync(CancellationToken ct)
    {
        var tenantId = tenantContext.RequireTenantId();
        var currency = await db.Tenants.Where(t => t.Id == tenantId).Select(t => t.Currency).SingleAsync(ct);

        var subscriptions = await db.MemberSubscriptions.AsNoTracking()
            .Where(s => s.Status != SubscriptionStatus.Incomplete)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new GymSubscriptionDto(
                s.Id, s.UserId, s.User!.FullName, s.User.Email, s.Plan!.Name, s.Plan.Price, s.Plan.Interval,
                s.Status, s.CurrentPeriodEnd, s.CancelAtPeriodEnd, s.CreatedAt))
            .ToListAsync(ct);

        var paying = subscriptions.Where(s => s.Status is SubscriptionStatus.Active or SubscriptionStatus.PastDue).ToList();
        var mrr = paying.Sum(s => s.Interval == BillingInterval.Year ? s.Price / 12 : s.Price);

        return new BillingOverviewDto(
            paying.Select(s => s.UserId).Distinct().Count(), Math.Round(mrr, 2), currency, subscriptions);
    }

    public async Task<bool> ApplyUpdateAsync(SubscriptionUpdate update, CancellationToken ct)
    {
        var status = MapStatus(update.StripeStatus);

        var subscription = await db.MemberSubscriptions.IgnoreQueryFilters()
            .SingleOrDefaultAsync(s => s.StripeSubscriptionId == update.StripeSubscriptionId, ct);

        if (subscription is null)
        {
            if (update.TenantId is not { } tenantId || update.UserId is not { } userId || update.PlanId is not { } planId)
            {
                logger.LogWarning("Ignoring subscription {Id} without GymFlow metadata", update.StripeSubscriptionId);
                return false;
            }

            var planExists = await db.MembershipPlans.IgnoreQueryFilters()
                .AnyAsync(p => p.Id == planId && p.TenantId == tenantId, ct);
            var isMember = await db.TenantUsers.IgnoreQueryFilters()
                .AnyAsync(tu => tu.TenantId == tenantId && tu.UserId == userId, ct);

            if (!planExists || !isMember)
            {
                logger.LogWarning("Ignoring subscription {Id} with unknown gym, plan or member", update.StripeSubscriptionId);
                return false;
            }

            subscription = new MemberSubscription
            {
                TenantId = tenantId,
                UserId = userId,
                PlanId = planId,
                StripeSubscriptionId = update.StripeSubscriptionId,
                LastEventAt = DateTimeOffset.UnixEpoch
            };
            db.MemberSubscriptions.Add(subscription);
        }

        var applied = subscription.Apply(
            status, update.CurrentPeriodEnd, update.CancelAtPeriodEnd, update.CustomerId, update.EventAt);

        if (!applied)
        {
            logger.LogInformation("Skipping stale event for subscription {Id}", update.StripeSubscriptionId);
            return false;
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Subscription {Id} is now {Status}", update.StripeSubscriptionId, subscription.Status);
        return true;
    }

    public static SubscriptionStatus MapStatus(string stripeStatus) => stripeStatus switch
    {
        "active" or "trialing" => SubscriptionStatus.Active,
        "past_due" or "unpaid" => SubscriptionStatus.PastDue,
        "canceled" or "incomplete_expired" => SubscriptionStatus.Canceled,
        _ => SubscriptionStatus.Incomplete
    };
}
