using GymFlow.Application.Common;
using GymFlow.Application.Common.Interfaces;
using GymFlow.Application.Tenancy;
using GymFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GymFlow.Application.Insights;

public class InsightsService(IApplicationDbContext db, ITenantContext tenantContext, TimeProvider timeProvider)
{
    public const int RevenueMonths = 6;
    public const int BookingWeeks = 8;
    public const int FillDays = 30;

    public async Task<InsightsDto> GetAsync(CancellationToken ct)
    {
        var tenantId = tenantContext.RequireTenantId();
        var gym = await db.Tenants.AsNoTracking()
            .Where(t => t.Id == tenantId)
            .Select(t => new { t.Currency, t.TimeZone })
            .SingleAsync(ct);

        var now = timeProvider.GetUtcNow();
        var timeZone = GymTime.FindTimeZone(gym.TimeZone);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, timeZone).DateTime);

        return new InsightsDto(
            gym.Currency,
            await GetRevenueAsync(today, now, timeZone, ct),
            await GetWeeklyBookingsAsync(today, timeZone, ct),
            await GetClassFillAsync(now, ct));
    }

    private async Task<IReadOnlyList<RevenuePointDto>> GetRevenueAsync(
        DateOnly today, DateTimeOffset now, TimeZoneInfo timeZone, CancellationToken ct)
    {
        var subscriptions = await db.MemberSubscriptions.AsNoTracking()
            .Where(s => s.Status != SubscriptionStatus.Incomplete)
            .Select(s => new { s.CreatedAt, s.CanceledAt, s.Plan!.Price, s.Plan.Interval })
            .ToListAsync(ct);

        var thisMonth = new DateOnly(today.Year, today.Month, 1);
        return Enumerable.Range(0, RevenueMonths)
            .Select(i => thisMonth.AddMonths(i - RevenueMonths + 1))
            .Select(month =>
            {
                var nextMonth = GymTime.ToUtc(month.AddMonths(1), TimeOnly.MinValue, timeZone);
                var at = nextMonth < now ? nextMonth : now;
                var mrr = subscriptions
                    .Where(s => s.CreatedAt < at && (s.CanceledAt is null || s.CanceledAt > at))
                    .Sum(s => s.Interval == BillingInterval.Year ? s.Price / 12 : s.Price);
                return new RevenuePointDto(month, Math.Round(mrr, 2));
            })
            .ToList();
    }

    private async Task<IReadOnlyList<WeeklyBookingsDto>> GetWeeklyBookingsAsync(
        DateOnly today, TimeZoneInfo timeZone, CancellationToken ct)
    {
        var thisWeek = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        var firstWeek = thisWeek.AddDays(-7 * (BookingWeeks - 1));
        var from = GymTime.ToUtc(firstWeek, TimeOnly.MinValue, timeZone);
        var to = GymTime.ToUtc(thisWeek.AddDays(7), TimeOnly.MinValue, timeZone);

        var bookings = await db.Bookings.AsNoTracking()
            .Where(b => !b.Session!.IsCancelled && b.Session.StartsAt >= from && b.Session.StartsAt < to
                && (b.Status == BookingStatus.Booked || b.Status == BookingStatus.Attended || b.Status == BookingStatus.NoShow))
            .Select(b => new { b.Session!.StartsAt, b.Status })
            .ToListAsync(ct);

        var byWeek = bookings.ToLookup(b =>
        {
            var day = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(b.StartsAt, timeZone).DateTime);
            return day.AddDays(-(((int)day.DayOfWeek + 6) % 7));
        });

        return Enumerable.Range(0, BookingWeeks)
            .Select(i => firstWeek.AddDays(7 * i))
            .Select(week => new WeeklyBookingsDto(
                week,
                byWeek[week].Count(b => b.Status == BookingStatus.Attended),
                byWeek[week].Count(b => b.Status == BookingStatus.NoShow),
                byWeek[week].Count(b => b.Status == BookingStatus.Booked)))
            .ToList();
    }

    private async Task<IReadOnlyList<ClassFillDto>> GetClassFillAsync(DateTimeOffset now, CancellationToken ct)
    {
        var from = now.AddDays(-FillDays);

        var rows = await db.ClassSessions.AsNoTracking()
            .Where(s => !s.IsCancelled && s.StartsAt >= from && s.StartsAt <= now)
            .GroupBy(s => s.GymClass!.Name)
            .Select(g => new { ClassName = g.Key, Sessions = g.Count(), Booked = g.Sum(s => s.BookedCount), Capacity = g.Sum(s => s.Capacity) })
            .ToListAsync(ct);

        return rows
            .Select(r => new ClassFillDto(
                r.ClassName, r.Sessions, r.Booked, r.Capacity,
                r.Capacity == 0 ? 0 : Math.Round((decimal)r.Booked / r.Capacity, 3)))
            .OrderByDescending(r => r.FillRate)
            .ThenBy(r => r.ClassName)
            .ToList();
    }
}
