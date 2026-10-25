using GymFlow.Domain.Enums;

namespace GymFlow.Application.Bookings;

public record BookingDto(
    Guid Id,
    Guid SessionId,
    string ClassName,
    string? TrainerName,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    BookingStatus Status,
    int? WaitlistPosition,
    bool IsSessionCancelled,
    DateTimeOffset BookedAt);

public enum BookingScope
{
    Upcoming,
    Past
}
