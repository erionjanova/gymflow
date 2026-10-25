namespace GymFlow.Application.Insights;

public record RevenuePointDto(DateOnly Month, decimal MonthlyRecurringRevenue);

public record WeeklyBookingsDto(DateOnly WeekStart, int Attended, int NoShow, int Booked);

public record ClassFillDto(string ClassName, int Sessions, int Booked, int Capacity, decimal FillRate);

public record InsightsDto(
    string Currency,
    IReadOnlyList<RevenuePointDto> Revenue,
    IReadOnlyList<WeeklyBookingsDto> WeeklyBookings,
    IReadOnlyList<ClassFillDto> ClassFill);
