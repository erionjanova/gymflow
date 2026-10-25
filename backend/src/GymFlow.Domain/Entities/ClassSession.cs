using GymFlow.Domain.Common;
using GymFlow.Domain.Enums;
using GymFlow.Domain.Exceptions;

namespace GymFlow.Domain.Entities;

public class ClassSession : TenantEntity
{
    public Guid GymClassId { get; set; }
    public GymClass? GymClass { get; set; }
    public Guid? TrainerId { get; set; }
    public User? Trainer { get; set; }
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public int Capacity { get; set; }
    public int BookedCount { get; set; }
    public int WaitlistCount { get; set; }
    public bool IsCancelled { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public uint Version { get; set; }

    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();

    public int SpotsLeft => Math.Max(0, Capacity - BookedCount);

    public Booking Book(Guid userId, DateTimeOffset now)
    {
        EnsureOpen(now);

        var hasSpot = BookedCount < Capacity;
        if (hasSpot)
            BookedCount++;
        else
            WaitlistCount++;

        return new Booking
        {
            TenantId = TenantId,
            SessionId = Id,
            UserId = userId,
            Status = hasSpot ? BookingStatus.Booked : BookingStatus.Waitlisted
        };
    }

    public Booking? CancelBooking(Booking booking, IEnumerable<Booking> waitlist, DateTimeOffset now)
    {
        if (booking.SessionId != Id)
            throw new DomainException("booking_wrong_session", "This booking belongs to another session.");

        if (now >= StartsAt)
            throw new DomainException("session_started", "This session has already started.");

        var heldSpot = booking.Status == BookingStatus.Booked;
        booking.Cancel(now);

        if (!heldSpot)
        {
            WaitlistCount--;
            return null;
        }

        BookedCount--;
        return FillFromWaitlist(waitlist, now).FirstOrDefault();
    }

    public IReadOnlyList<Booking> ChangeCapacity(int capacity, IEnumerable<Booking> waitlist, DateTimeOffset now)
    {
        if (capacity < 1)
            throw new DomainException("capacity_invalid", "Capacity must be at least 1.");

        if (capacity < BookedCount)
            throw new DomainException(
                "capacity_below_bookings", $"{BookedCount} people are already booked. Capacity can't go below that.");

        Capacity = capacity;
        return now < StartsAt && !IsCancelled ? FillFromWaitlist(waitlist, now) : [];
    }

    public void Cancel(IEnumerable<Booking> activeBookings, DateTimeOffset now)
    {
        if (IsCancelled)
            return;

        if (now >= StartsAt)
            throw new DomainException("session_started", "A session that already started can't be cancelled.");

        foreach (var booking in activeBookings.Where(b => b.SessionId == Id && b.IsActive))
            booking.Cancel(now);

        IsCancelled = true;
        CancelledAt = now;
        BookedCount = 0;
        WaitlistCount = 0;
    }

    private List<Booking> FillFromWaitlist(IEnumerable<Booking> waitlist, DateTimeOffset now)
    {
        var promoted = new List<Booking>();

        foreach (var next in waitlist
                     .Where(b => b.SessionId == Id && b.Status == BookingStatus.Waitlisted)
                     .OrderBy(b => b.CreatedAt)
                     .ThenBy(b => b.Id))
        {
            if (BookedCount >= Capacity)
                break;

            next.Promote(now);
            BookedCount++;
            WaitlistCount--;
            promoted.Add(next);
        }

        return promoted;
    }

    private void EnsureOpen(DateTimeOffset now)
    {
        if (IsCancelled)
            throw new DomainException("session_cancelled", "This session was cancelled.");

        if (now >= StartsAt)
            throw new DomainException("session_started", "This session has already started.");
    }
}
