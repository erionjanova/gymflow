using GymFlow.Application.Insights;
using GymFlow.Application.Tenancy;
using GymFlow.Domain.Entities;
using GymFlow.Domain.Enums;

namespace GymFlow.IntegrationTests;

[Collection(PostgresCollection.Name)]
public class InsightsTests(PostgresFixture fixture)
{
    private async Task<InsightsDto> GetAsync(Guid tenantId)
    {
        await using var db = fixture.CreateContext(tenantId, TenantRole.Owner);
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(tenantId, "test");
        tenantContext.SetRole(TenantRole.Owner);
        return await new InsightsService(db, tenantContext, TimeProvider.System).GetAsync(default);
    }

    [Fact]
    public async Task Revenue_counts_only_memberships_active_at_each_month_end()
    {
        var gym = await fixture.SeedGymAsync("Revenue Gym", "Basic", "Gold");
        var users = await fixture.SeedUsersAsync(gym.TenantId, 3);
        var now = DateTimeOffset.UtcNow;

        await using (var db = fixture.CreateContext(gym.TenantId))
        {
            db.MemberSubscriptions.AddRange(
                new MemberSubscription
                {
                    UserId = users[0], PlanId = gym.PlanIds[0], Status = SubscriptionStatus.Active,
                    StripeSubscriptionId = $"sub_{Guid.NewGuid():N}", CreatedAt = now.AddDays(-100)
                },
                new MemberSubscription
                {
                    UserId = users[1], PlanId = gym.PlanIds[1], Status = SubscriptionStatus.Canceled,
                    StripeSubscriptionId = $"sub_{Guid.NewGuid():N}", CreatedAt = now.AddDays(-70), CanceledAt = now.AddDays(-10)
                },
                new MemberSubscription
                {
                    UserId = users[2], PlanId = gym.PlanIds[1], Status = SubscriptionStatus.Incomplete,
                    StripeSubscriptionId = $"sub_{Guid.NewGuid():N}", CreatedAt = now.AddDays(-5)
                });
            await db.SaveChangesAsync();
        }

        var revenue = (await GetAsync(gym.TenantId)).Revenue;

        Assert.Equal(InsightsService.RevenueMonths, revenue.Count);
        Assert.Equal(0, revenue[0].MonthlyRecurringRevenue);
        Assert.Contains(revenue, p => p.MonthlyRecurringRevenue == 41);
        Assert.Equal(20, revenue[^1].MonthlyRecurringRevenue);
    }

    [Fact]
    public async Task Weekly_bookings_and_class_fill_skip_cancelled_sessions()
    {
        var gym = await fixture.SeedGymAsync("Insights Gym");
        var users = await fixture.SeedUsersAsync(gym.TenantId, 4);
        var now = DateTimeOffset.UtcNow;

        await using (var db = fixture.CreateContext(gym.TenantId))
        {
            var spin = new GymClass { Name = "Spin", DurationMinutes = 60, DefaultCapacity = 4 };
            var yoga = new GymClass { Name = "Yoga", DurationMinutes = 60, DefaultCapacity = 4 };

            ClassSession Session(GymClass gymClass, DateTimeOffset startsAt, bool cancelled = false) => new()
            {
                GymClass = gymClass, StartsAt = startsAt, EndsAt = startsAt.AddHours(1), Capacity = 4, IsCancelled = cancelled
            };

            var past = Session(spin, now.AddDays(-14));
            var quiet = Session(yoga, now.AddDays(-13));
            var cancelled = Session(spin, now.AddDays(-12), cancelled: true);
            var upcoming = Session(spin, now.AddDays(10));

            void Book(ClassSession session, Guid userId, BookingStatus status)
            {
                db.Bookings.Add(new Booking { Session = session, UserId = userId, Status = status });
                if (status is not BookingStatus.Cancelled and not BookingStatus.Waitlisted && !session.IsCancelled)
                    session.BookedCount++;
            }

            Book(past, users[0], BookingStatus.Attended);
            Book(past, users[1], BookingStatus.Attended);
            Book(past, users[2], BookingStatus.NoShow);
            Book(past, users[3], BookingStatus.Cancelled);
            Book(quiet, users[0], BookingStatus.Attended);
            Book(cancelled, users[1], BookingStatus.Cancelled);
            Book(upcoming, users[2], BookingStatus.Booked);

            db.ClassSessions.AddRange(past, quiet, cancelled, upcoming);
            await db.SaveChangesAsync();
        }

        var insights = await GetAsync(gym.TenantId);

        Assert.Equal(InsightsService.BookingWeeks, insights.WeeklyBookings.Count);
        Assert.Equal(3, insights.WeeklyBookings.Sum(w => w.Attended));
        Assert.Equal(1, insights.WeeklyBookings.Sum(w => w.NoShow));
        Assert.Equal(0, insights.WeeklyBookings.Sum(w => w.Booked));
        Assert.Equal(DayOfWeek.Monday, insights.WeeklyBookings[0].WeekStart.DayOfWeek);

        Assert.Collection(insights.ClassFill,
            spin => Assert.Equal(("Spin", 1, 3, 0.75m), (spin.ClassName, spin.Sessions, spin.Booked, spin.FillRate)),
            yoga => Assert.Equal(("Yoga", 1, 1, 0.25m), (yoga.ClassName, yoga.Sessions, yoga.Booked, yoga.FillRate)));
    }
}
