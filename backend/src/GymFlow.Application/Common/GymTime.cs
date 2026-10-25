namespace GymFlow.Application.Common;

public static class GymTime
{
    public static bool IsValidTimeZone(string id) => TimeZoneInfo.TryFindSystemTimeZoneById(id, out _);

    public static TimeZoneInfo FindTimeZone(string id) => TimeZoneInfo.FindSystemTimeZoneById(id);

    public static DateTimeOffset ToUtc(DateOnly date, TimeOnly time, TimeZoneInfo timeZone)
    {
        var local = date.ToDateTime(time, DateTimeKind.Unspecified);
        if (timeZone.IsInvalidTime(local))
            local = local.AddHours(1);

        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, timeZone), TimeSpan.Zero);
    }

    public static IEnumerable<DateTimeOffset> WeeklySlots(
        DateOnly from, DateOnly to, IReadOnlyCollection<DayOfWeek> days, TimeOnly time, TimeZoneInfo timeZone)
    {
        for (var date = from; date <= to; date = date.AddDays(1))
        {
            if (days.Contains(date.DayOfWeek))
                yield return ToUtc(date, time, timeZone);
        }
    }
}
