using GymFlow.Application.Billing;
using GymFlow.Application.Bookings;
using GymFlow.Application.Common;
using GymFlow.Application.Common.Exceptions;
using GymFlow.Application.Tenancy;
using GymFlow.Domain.Enums;
using GymFlow.Domain.Exceptions;
using GymFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GymFlow.IntegrationTests;

[Collection(PostgresCollection.Name)]
public class BillingTests(PostgresFixture fixture)
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    private static TenantContext Tenant(Guid? tenantId, TenantRole role = TenantRole.Member)
    {
        var context = new TenantContext();
        if (tenantId is { } id)
        {
            context.SetTenant(id, "test");
            context.SetRole(role);
        }

        return context;
    }

    private static BillingService Billing(
        ApplicationDbContext db, Guid? tenantId, Guid? userId, FakePaymentGateway? gateway = null,
        TenantRole role = TenantRole.Member) =>
        new(db, Tenant(tenantId, role), new FakeCurrentUser(userId), gateway ?? new FakePaymentGateway(),
            Options.Create(new AppOptions { FrontendUrl = "http://app.test" }), NullLogger<BillingService>.Instance);

    private static SubscriptionUpdate Update(
        SeededGym gym, string stripeId, string status, DateTimeOffset at, bool cancelAtEnd = false) =>
        new(stripeId, status, "cus_123", at.AddMonths(1), cancelAtEnd, at, gym.TenantId, gym.MemberId, gym.PlanIds[0]);

    private async Task<bool> WebhookAsync(SubscriptionUpdate update)
    {
        await using var db = fixture.CreateContext();
        return await Billing(db, null, null).ApplyUpdateAsync(update, default);
    }

    private async Task<MySubscriptionDto?> MineAsync(SeededGym gym)
    {
        await using var db = fixture.CreateContext(gym.TenantId, TenantRole.Member);
        return await Billing(db, gym.TenantId, gym.MemberId).GetMineAsync(default);
    }

    [Fact]
    public async Task Webhook_creates_and_updates_the_members_subscription()
    {
        var gym = await fixture.SeedGymAsync("Billing Gym", "Basic");
        var stripeId = $"sub_{Guid.NewGuid():N}";

        Assert.True(await WebhookAsync(Update(gym, stripeId, "incomplete", T0)));
        Assert.Null(await MineAsync(gym));

        Assert.True(await WebhookAsync(Update(gym, stripeId, "active", T0.AddSeconds(5))));
        var mine = await MineAsync(gym);
        Assert.Equal(SubscriptionStatus.Active, mine!.Status);
        Assert.Equal("Basic", mine.PlanName);

        Assert.True(await WebhookAsync(Update(gym, stripeId, "past_due", T0.AddDays(30))));
        Assert.Equal(SubscriptionStatus.PastDue, (await MineAsync(gym))!.Status);

        Assert.True(await WebhookAsync(Update(gym, stripeId, "canceled", T0.AddDays(40))));
        Assert.Equal(SubscriptionStatus.Canceled, (await MineAsync(gym))!.Status);
    }

    [Fact]
    public async Task Older_events_arriving_late_are_ignored()
    {
        var gym = await fixture.SeedGymAsync("Order Gym", "Basic");
        var stripeId = $"sub_{Guid.NewGuid():N}";

        await WebhookAsync(Update(gym, stripeId, "canceled", T0.AddDays(10)));
        var applied = await WebhookAsync(Update(gym, stripeId, "active", T0));

        Assert.False(applied);
        Assert.Equal(SubscriptionStatus.Canceled, (await MineAsync(gym))!.Status);
    }

    [Fact]
    public async Task Webhooks_with_unknown_or_mismatched_metadata_are_ignored()
    {
        var gym = await fixture.SeedGymAsync("Meta Gym", "Basic");
        var other = await fixture.SeedGymAsync("Meta Other", "Other plan");

        Assert.False(await WebhookAsync(Update(gym, $"sub_{Guid.NewGuid():N}", "active", T0) with { TenantId = null }));
        Assert.False(await WebhookAsync(Update(gym, $"sub_{Guid.NewGuid():N}", "active", T0) with { PlanId = other.PlanIds[0] }));
        Assert.False(await WebhookAsync(Update(gym, $"sub_{Guid.NewGuid():N}", "active", T0) with { UserId = other.MemberId }));

        Assert.Null(await MineAsync(gym));
    }

    [Fact]
    public async Task Checkout_sends_the_plan_details_and_blocks_double_subscriptions()
    {
        var gym = await fixture.SeedGymAsync("Checkout Gym", "Premium");
        var gateway = new FakePaymentGateway();

        await using (var db = fixture.CreateContext(gym.TenantId, TenantRole.Member))
        {
            var checkout = await Billing(db, gym.TenantId, gym.MemberId, gateway).StartCheckoutAsync(gym.PlanIds[0], default);
            Assert.StartsWith("https://checkout.stripe.test/", checkout.Url);
        }

        var request = Assert.Single(gateway.Checkouts);
        Assert.Equal((gym.TenantId, gym.MemberId, gym.PlanIds[0]), (request.TenantId, request.UserId, request.PlanId));
        Assert.Equal("Premium", request.PlanName);
        Assert.Equal(20m, request.Price);
        Assert.StartsWith("http://app.test/g/", request.SuccessUrl);

        await WebhookAsync(Update(gym, $"sub_{Guid.NewGuid():N}", "active", T0));

        await using (var db = fixture.CreateContext(gym.TenantId, TenantRole.Member))
        {
            var ex = await Assert.ThrowsAsync<ConflictException>(() =>
                Billing(db, gym.TenantId, gym.MemberId, gateway).StartCheckoutAsync(gym.PlanIds[0], default));
            Assert.Equal("already_subscribed", ex.Code);
        }
    }

    [Fact]
    public async Task Checkout_fails_clearly_when_payments_are_not_configured()
    {
        var gym = await fixture.SeedGymAsync("No Stripe Gym", "Basic");

        await using var db = fixture.CreateContext(gym.TenantId, TenantRole.Member);
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            Billing(db, gym.TenantId, gym.MemberId, new FakePaymentGateway(configured: false))
                .StartCheckoutAsync(gym.PlanIds[0], default));

        Assert.Equal("payments_not_configured", ex.Code);
    }

    [Fact]
    public async Task Member_cancels_at_period_end_and_owner_sees_revenue()
    {
        var gym = await fixture.SeedGymAsync("Revenue Gym", "Monthly");
        var stripeId = $"sub_{Guid.NewGuid():N}";
        await WebhookAsync(Update(gym, stripeId, "active", T0));
        var gateway = new FakePaymentGateway();

        await using (var db = fixture.CreateContext(gym.TenantId, TenantRole.Member))
        {
            var mine = await Billing(db, gym.TenantId, gym.MemberId, gateway).SetCancelAtPeriodEndAsync(true, default);
            Assert.True(mine.CancelAtPeriodEnd);
        }

        Assert.Equal((stripeId, true), Assert.Single(gateway.CancelCalls));

        await using (var db = fixture.CreateContext(gym.TenantId, TenantRole.Owner))
        {
            var overview = await Billing(db, gym.TenantId, gym.OwnerId, role: TenantRole.Owner).GetOverviewAsync(default);
            Assert.Equal(1, overview.ActiveMembers);
            Assert.Equal(20m, overview.MonthlyRecurringRevenue);
            Assert.True(Assert.Single(overview.Subscriptions).CancelAtPeriodEnd);
        }
    }

    [Fact]
    public async Task Gym_can_require_an_active_membership_to_book()
    {
        var gym = await fixture.SeedGymAsync("Members Only Gym", "Basic");
        var sessionId = await fixture.SeedSessionAsync(gym.TenantId, capacity: 10);
        await using (var db = fixture.CreateContext())
        {
            var tenant = await db.Tenants.SingleAsync(t => t.Id == gym.TenantId);
            tenant.RequireActiveMembership = true;
            await db.SaveChangesAsync();
        }

        async Task<BookingDto> BookAsync(Guid userId, TenantRole role)
        {
            await using var db = fixture.CreateContext(gym.TenantId, role);
            return await new BookingService(db, Tenant(gym.TenantId, role), new FakeCurrentUser(userId), TimeProvider.System)
                .BookAsync(sessionId, default);
        }

        var blocked = await Assert.ThrowsAsync<ForbiddenException>(() => BookAsync(gym.MemberId, TenantRole.Member));
        Assert.Equal("membership_required", blocked.Code);

        Assert.Equal(BookingStatus.Booked, (await BookAsync(gym.OwnerId, TenantRole.Owner)).Status);

        await WebhookAsync(Update(gym, $"sub_{Guid.NewGuid():N}", "active", T0));
        Assert.Equal(BookingStatus.Booked, (await BookAsync(gym.MemberId, TenantRole.Member)).Status);
    }
}
