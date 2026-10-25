using GymFlow.Application.Common;

namespace GymFlow.Application.Tests.Common;

public class GymTimeTests
{
    private static readonly TimeZoneInfo Prishtina = GymTime.FindTimeZone("Europe/Tirane");

    [Fact]
    public void WeeklySlots_picks_only_the_chosen_days()
    {
        var slots = GymTime.WeeklySlots(
            new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 15),
            [DayOfWeek.Monday, DayOfWeek.Wednesday], new TimeOnly(18, 0), Prishtina).ToList();

        Assert.Equal(4, slots.Count);
        Assert.All(slots, s => Assert.Equal(TimeSpan.Zero, s.Offset));
        Assert.Equal(new DateTimeOffset(2026, 11, 2, 17, 0, 0, TimeSpan.Zero), slots[0]);
        Assert.Equal(new DateTimeOffset(2026, 11, 4, 17, 0, 0, TimeSpan.Zero), slots[1]);
    }

    [Fact]
    public void WeeklySlots_keep_the_local_time_across_daylight_saving_changes()
    {
        var slots = GymTime.WeeklySlots(
            new DateOnly(2026, 3, 23), new DateOnly(2026, 3, 30),
            [DayOfWeek.Monday], new TimeOnly(18, 0), Prishtina).ToList();

        Assert.Equal(new DateTimeOffset(2026, 3, 23, 17, 0, 0, TimeSpan.Zero), slots[0]);
        Assert.Equal(new DateTimeOffset(2026, 3, 30, 16, 0, 0, TimeSpan.Zero), slots[1]);
    }

    [Fact]
    public void ToUtc_moves_times_inside_the_spring_gap_forward()
    {
        var utc = GymTime.ToUtc(new DateOnly(2026, 3, 29), new TimeOnly(2, 30), Prishtina);

        Assert.Equal(new DateTimeOffset(2026, 3, 29, 1, 30, 0, TimeSpan.Zero), utc);
    }
}
