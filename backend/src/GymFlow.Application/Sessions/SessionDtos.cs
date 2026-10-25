using GymFlow.Domain.Enums;

namespace GymFlow.Application.Sessions;

public record SessionDto(
    Guid Id,
    Guid ClassId,
    string ClassName,
    Guid? TrainerId,
    string? TrainerName,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    int Capacity,
    int BookedCount,
    int SpotsLeft,
    int WaitlistCount,
    bool IsCancelled,
    BookingStatus? MyStatus,
    int? MyWaitlistPosition);

public record CreateSessionRequest(Guid GymClassId, Guid? TrainerId, DateTimeOffset StartsAt, int? Capacity);

public record ScheduleSessionsRequest(
    Guid GymClassId,
    Guid? TrainerId,
    DateOnly FromDate,
    DateOnly ToDate,
    DayOfWeek[] DaysOfWeek,
    TimeOnly StartTime,
    int? Capacity);

public record ScheduleSessionsResult(int Created, int Skipped, IReadOnlyList<SessionDto> Sessions);

public record UpdateSessionRequest(Guid? TrainerId, DateTimeOffset StartsAt, int Capacity);

public record RosterEntryDto(
    Guid BookingId,
    Guid UserId,
    string FullName,
    string Email,
    BookingStatus Status,
    DateTimeOffset BookedAt,
    int? WaitlistPosition);

public record MarkAttendanceRequest(bool Attended);
