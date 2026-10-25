using GymFlow.Domain.Entities;
using GymFlow.Domain.Enums;
using GymFlow.Domain.Exceptions;

namespace GymFlow.Application.Tests.Domain;

public class ClassSessionTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);

    private static ClassSession NewSession(int capacity) => new()
    {
        TenantId = Guid.NewGuid(),
        StartsAt = Now.AddHours(9),
        EndsAt = Now.AddHours(10),
        Capacity = capacity
    };

    private static List<Booking> BookMany(ClassSession session, int count)
    {
        var bookings = new List<Booking>();
        for (var i = 0; i < count; i++)
        {
            var booking = session.Book(Guid.NewGuid(), Now);
            booking.CreatedAt = Now.AddSeconds(i);
            bookings.Add(booking);
        }

        return bookings;
    }

    [Fact]
    public void Book_takes_a_spot_while_there_is_room()
    {
        var session = NewSession(2);

        var booking = session.Book(Guid.NewGuid(), Now);

        Assert.Equal(BookingStatus.Booked, booking.Status);
        Assert.Equal(session.Id, booking.SessionId);
        Assert.Equal(session.TenantId, booking.TenantId);
        Assert.Equal(1, session.BookedCount);
        Assert.Equal(1, session.SpotsLeft);
    }

    [Fact]
    public void Book_goes_to_the_waitlist_when_full()
    {
        var session = NewSession(1);
        session.Book(Guid.NewGuid(), Now);

        var second = session.Book(Guid.NewGuid(), Now);

        Assert.Equal(BookingStatus.Waitlisted, second.Status);
        Assert.Equal(1, session.BookedCount);
        Assert.Equal(1, session.WaitlistCount);
    }

    [Fact]
    public void Book_is_rejected_for_cancelled_or_started_sessions()
    {
        var cancelled = NewSession(5);
        cancelled.Cancel([], Now);
        var started = NewSession(5);

        Assert.Equal("session_cancelled", Assert.Throws<DomainException>(() => cancelled.Book(Guid.NewGuid(), Now)).Code);
        Assert.Equal("session_started", Assert.Throws<DomainException>(() => started.Book(Guid.NewGuid(), started.StartsAt)).Code);
    }

    [Fact]
    public void Cancelling_a_spot_promotes_the_first_person_on_the_waitlist()
    {
        var session = NewSession(1);
        var bookings = BookMany(session, 3);

        var promoted = session.CancelBooking(bookings[0], bookings, Now);

        Assert.Same(bookings[1], promoted);
        Assert.Equal(BookingStatus.Cancelled, bookings[0].Status);
        Assert.Equal(BookingStatus.Booked, bookings[1].Status);
        Assert.Equal(Now, bookings[1].PromotedAt);
        Assert.Equal(BookingStatus.Waitlisted, bookings[2].Status);
        Assert.Equal(1, session.BookedCount);
        Assert.Equal(1, session.WaitlistCount);
    }

    [Fact]
    public void Leaving_the_waitlist_does_not_free_a_spot()
    {
        var session = NewSession(1);
        var bookings = BookMany(session, 3);

        var promoted = session.CancelBooking(bookings[2], bookings, Now);

        Assert.Null(promoted);
        Assert.Equal(1, session.BookedCount);
        Assert.Equal(1, session.WaitlistCount);
        Assert.Equal(BookingStatus.Waitlisted, bookings[1].Status);
    }

    [Fact]
    public void Cancelling_after_the_start_is_not_allowed()
    {
        var session = NewSession(1);
        var booking = session.Book(Guid.NewGuid(), Now);

        var ex = Assert.Throws<DomainException>(() => session.CancelBooking(booking, [], session.StartsAt));

        Assert.Equal("session_started", ex.Code);
    }

    [Fact]
    public void Raising_capacity_promotes_from_the_waitlist_in_order()
    {
        var session = NewSession(1);
        var bookings = BookMany(session, 4);

        var promoted = session.ChangeCapacity(3, bookings, Now);

        Assert.Equal([bookings[1], bookings[2]], promoted);
        Assert.Equal(3, session.BookedCount);
        Assert.Equal(1, session.WaitlistCount);
        Assert.Equal(BookingStatus.Waitlisted, bookings[3].Status);
    }

    [Fact]
    public void Capacity_cannot_drop_below_booked_people()
    {
        var session = NewSession(3);
        BookMany(session, 3);

        var ex = Assert.Throws<DomainException>(() => session.ChangeCapacity(2, [], Now));

        Assert.Equal("capacity_below_bookings", ex.Code);
        Assert.Equal(3, session.Capacity);
    }

    [Fact]
    public void Cancelling_the_session_cancels_every_active_booking()
    {
        var session = NewSession(1);
        var bookings = BookMany(session, 2);

        session.Cancel(bookings, Now);

        Assert.True(session.IsCancelled);
        Assert.All(bookings, b => Assert.Equal(BookingStatus.Cancelled, b.Status));
        Assert.Equal(0, session.BookedCount);
        Assert.Equal(0, session.WaitlistCount);
    }

    [Fact]
    public void Attendance_can_only_be_marked_for_confirmed_bookings()
    {
        var session = NewSession(1);
        var bookings = BookMany(session, 2);

        bookings[0].MarkAttendance(true, Now);
        bookings[0].MarkAttendance(false, Now);

        Assert.Equal(BookingStatus.NoShow, bookings[0].Status);
        Assert.Throws<DomainException>(() => bookings[1].MarkAttendance(true, Now));
    }
}
