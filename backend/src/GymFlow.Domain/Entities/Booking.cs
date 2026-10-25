using GymFlow.Domain.Common;
using GymFlow.Domain.Enums;
using GymFlow.Domain.Exceptions;

namespace GymFlow.Domain.Entities;

public class Booking : TenantEntity
{
    public Guid SessionId { get; set; }
    public ClassSession? Session { get; set; }
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public BookingStatus Status { get; set; } = BookingStatus.Booked;
    public DateTimeOffset? PromotedAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public DateTimeOffset? AttendanceMarkedAt { get; set; }

    public bool IsActive => Status is BookingStatus.Booked or BookingStatus.Waitlisted;

    public void Cancel(DateTimeOffset now)
    {
        if (!IsActive)
            throw new DomainException("booking_not_active", "This booking is no longer active.");

        Status = BookingStatus.Cancelled;
        CancelledAt = now;
    }

    public void Promote(DateTimeOffset now)
    {
        if (Status != BookingStatus.Waitlisted)
            throw new DomainException("booking_not_waitlisted", "Only waitlisted bookings can be promoted.");

        Status = BookingStatus.Booked;
        PromotedAt = now;
    }

    public void MarkAttendance(bool attended, DateTimeOffset now)
    {
        if (Status is not (BookingStatus.Booked or BookingStatus.Attended or BookingStatus.NoShow))
            throw new DomainException("booking_not_confirmed", "Attendance can only be marked for confirmed bookings.");

        Status = attended ? BookingStatus.Attended : BookingStatus.NoShow;
        AttendanceMarkedAt = now;
    }
}
