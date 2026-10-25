using GymFlow.Application.Billing;
using GymFlow.Application.Bookings;
using GymFlow.Application.Common;
using GymFlow.Application.Common.Exceptions;
using GymFlow.Application.Common.Interfaces;
using GymFlow.Application.Notifications;
using GymFlow.Application.Tenancy;
using GymFlow.Domain.Enums;
using GymFlow.Domain.Exceptions;
using GymFlow.Infrastructure.Payments;
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
        ApplicationDbContext db, Guid? tenantId, Guid? userId, IPaymentGateway? gateway = null,
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
        Assert.EndsWith("session_id={CHECKOUT_SESSION_ID}", request.SuccessUrl);

        await WebhookAsync(Update(gym, $"sub_{Guid.NewGuid():N}", "active", T0));

        await using (var db = fixture.CreateContext(gym.TenantId, TenantRole.Member))
        {
            var ex = await Assert.ThrowsAsync<ConflictException>(() =>
                Billing(db, gym.TenantId, gym.MemberId, gateway).StartCheckoutAsync(gym.PlanIds[0], default));
            Assert.Equal("already_subscribed", ex.Code);
        }
    }

    [Fact]
    public async Task Returning_from_checkout_activates_without_waiting_for_the_webhook()
    {
        var gym = await fixture.SeedGymAsync("Sync Gym", "Basic");
        var stripeId = $"sub_{Guid.NewGuid():N}";
        var gateway = new FakePaymentGateway();
        gateway.CompletedCheckouts["cs_test_paid"] = Update(gym, stripeId, "active", T0.AddMinutes(1));

        await using (var db = fixture.CreateContext(gym.TenantId, TenantRole.Member))
        {
            var synced = await Billing(db, gym.TenantId, gym.MemberId, gateway).SyncCheckoutAsync("cs_test_paid", default);
            Assert.Equal(SubscriptionStatus.Active, synced!.Status);
        }

        Assert.False(await WebhookAsync(Update(gym, stripeId, "incomplete", T0)));
        Assert.True(await WebhookAsync(Update(gym, stripeId, "active", T0.AddMinutes(2))));
        Assert.Equal(SubscriptionStatus.Active, (await MineAsync(gym))!.Status);

        await using (var db = fixture.CreateContext(gym.TenantId, TenantRole.Member))
        {
            var again = await Billing(db, gym.TenantId, gym.MemberId, gateway).SyncCheckoutAsync("cs_test_paid", default);
            Assert.Equal(SubscriptionStatus.Active, again!.Status);
            Assert.Equal(1, await db.MemberSubscriptions.CountAsync(s => s.StripeSubscriptionId == stripeId));
        }
    }

    [Fact]
    public async Task A_member_cannot_claim_someone_elses_checkout()
    {
        var gym = await fixture.SeedGymAsync("Claim Gym", "Basic");
        var intruder = (await fixture.SeedUsersAsync(gym.TenantId, 1))[0];
        var gateway = new FakePaymentGateway();
        gateway.CompletedCheckouts["cs_test_theirs"] = Update(gym, $"sub_{Guid.NewGuid():N}", "active", T0);

        await using var db = fixture.CreateContext(gym.TenantId, TenantRole.Member);
        var ex = await Assert.ThrowsAsync<ForbiddenException>(() =>
            Billing(db, gym.TenantId, intruder, gateway).SyncCheckoutAsync("cs_test_theirs", default));
        Assert.Equal("checkout_not_yours", ex.Code);

        await Assert.ThrowsAsync<DomainException>(() =>
            Billing(db, gym.TenantId, intruder, gateway).SyncCheckoutAsync("not-a-session", default));
    }

    [Fact]
    public async Task Member_can_switch_plan_after_cancelling_and_it_renews_again()
    {
        var gym = await fixture.SeedGymAsync("Switch Gym", "Basic", "Premium");
        var stripeId = $"sub_{Guid.NewGuid():N}";
        var gateway = new FakePaymentGateway();
        await WebhookAsync(Update(gym, stripeId, "active", DateTimeOffset.UtcNow.AddMinutes(-5)));

        await using (var db = fixture.CreateContext(gym.TenantId, TenantRole.Member))
            await Billing(db, gym.TenantId, gym.MemberId, gateway).SetCancelAtPeriodEndAsync(true, default);

        await using (var db = fixture.CreateContext(gym.TenantId, TenantRole.Member))
        {
            var switched = await Billing(db, gym.TenantId, gym.MemberId, gateway).ChangePlanAsync(gym.PlanIds[1], default);
            Assert.Equal(("Premium", false, SubscriptionStatus.Active), (switched.PlanName, switched.CancelAtPeriodEnd, switched.Status));
        }

        var change = Assert.Single(gateway.PlanChanges);
        Assert.Equal((stripeId, gym.PlanIds[1], 21m), (change.SubscriptionId, change.PlanId, change.Price));

        await using (var db = fixture.CreateContext(gym.TenantId, TenantRole.Member))
        {
            var ex = await Assert.ThrowsAsync<ConflictException>(() =>
                Billing(db, gym.TenantId, gym.MemberId, gateway).ChangePlanAsync(gym.PlanIds[1], default));
            Assert.Equal("same_plan", ex.Code);
        }
    }

    [Fact]
    public async Task Choosing_the_current_plan_after_cancelling_keeps_it()
    {
        var gym = await fixture.SeedGymAsync("Keep Gym", "Basic");
        var gateway = new FakePaymentGateway();
        await WebhookAsync(Update(gym, $"sub_{Guid.NewGuid():N}", "active", DateTimeOffset.UtcNow.AddMinutes(-5)));

        await using (var db = fixture.CreateContext(gym.TenantId, TenantRole.Member))
            await Billing(db, gym.TenantId, gym.MemberId, gateway).SetCancelAtPeriodEndAsync(true, default);

        await using (var db = fixture.CreateContext(gym.TenantId, TenantRole.Member))
        {
            var kept = await Billing(db, gym.TenantId, gym.MemberId, gateway).ChangePlanAsync(gym.PlanIds[0], default);
            Assert.False(kept.CancelAtPeriodEnd);
        }

        Assert.Empty(gateway.PlanChanges);
        Assert.Equal([(gateway.CancelCalls[0].SubscriptionId, true), (gateway.CancelCalls[0].SubscriptionId, false)], gateway.CancelCalls);
    }

    [Fact]
    public async Task Webhook_with_a_new_plan_moves_the_membership_to_it()
    {
        var gym = await fixture.SeedGymAsync("Webhook Plan Gym", "Basic", "Premium");
        var stripeId = $"sub_{Guid.NewGuid():N}";

        await WebhookAsync(Update(gym, stripeId, "active", T0));
        await WebhookAsync(Update(gym, stripeId, "active", T0.AddDays(3)) with { PlanId = gym.PlanIds[1] });
        Assert.Equal("Premium", (await MineAsync(gym))!.PlanName);

        Assert.False(await WebhookAsync(Update(gym, stripeId, "active", T0.AddDays(1))));
        Assert.Equal("Premium", (await MineAsync(gym))!.PlanName);
    }

    [Fact]
    public async Task Demo_payments_run_the_whole_membership_flow_without_stripe()
    {
        var gym = await fixture.SeedGymAsync("Demo Pay Gym", "Basic", "Premium");
        var intruder = (await fixture.SeedUsersAsync(gym.TenantId, 1))[0];
        var gateway = new DemoPaymentGateway(TimeProvider.System);

        string sessionId;
        await using (var db = fixture.CreateContext(gym.TenantId, TenantRole.Member))
        {
            var checkout = await Billing(db, gym.TenantId, gym.MemberId, gateway).StartCheckoutAsync(gym.PlanIds[0], default);
            Assert.StartsWith("http://app.test/g/", checkout.Url);
            sessionId = checkout.Url.Split("session_id=")[1];
            Assert.StartsWith(DemoPaymentGateway.SessionPrefix, sessionId);
        }

        await using (var db = fixture.CreateContext(gym.TenantId, TenantRole.Member))
        {
            var ex = await Assert.ThrowsAsync<ForbiddenException>(() =>
                Billing(db, gym.TenantId, intruder, gateway).SyncCheckoutAsync(sessionId, default));
            Assert.Equal("checkout_not_yours", ex.Code);
        }

        await using (var db = fixture.CreateContext(gym.TenantId, TenantRole.Member))
        {
            var mine = await Billing(db, gym.TenantId, gym.MemberId, gateway).SyncCheckoutAsync(sessionId, default);
            Assert.Equal(("Basic", SubscriptionStatus.Active), (mine!.PlanName, mine.Status));
            Assert.True(mine.CurrentPeriodEnd > DateTimeOffset.UtcNow.AddDays(27));
        }

        await using (var db = fixture.CreateContext(gym.TenantId, TenantRole.Member))
            await Billing(db, gym.TenantId, gym.MemberId, gateway).SetCancelAtPeriodEndAsync(true, default);

        await using (var db = fixture.CreateContext(gym.TenantId, TenantRole.Member))
        {
            var switched = await Billing(db, gym.TenantId, gym.MemberId, gateway).ChangePlanAsync(gym.PlanIds[1], default);
            Assert.Equal(("Premium", false), (switched.PlanName, switched.CancelAtPeriodEnd));
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
            return await new BookingService(db, Tenant(gym.TenantId, role), new FakeCurrentUser(userId), TimeProvider.System,
                    new SessionNotifier(db, new CapturingEmailSender(), Options.Create(new AppOptions())))
                .BookAsync(sessionId, default);
        }

        var blocked = await Assert.ThrowsAsync<ForbiddenException>(() => BookAsync(gym.MemberId, TenantRole.Member));
        Assert.Equal("membership_required", blocked.Code);

        Assert.Equal(BookingStatus.Booked, (await BookAsync(gym.OwnerId, TenantRole.Owner)).Status);

        await WebhookAsync(Update(gym, $"sub_{Guid.NewGuid():N}", "active", T0));
        Assert.Equal(BookingStatus.Booked, (await BookAsync(gym.MemberId, TenantRole.Member)).Status);
    }
}
