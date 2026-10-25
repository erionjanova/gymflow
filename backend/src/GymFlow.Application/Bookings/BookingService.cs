using GymFlow.Application.Common;
using GymFlow.Application.Common.Exceptions;
using GymFlow.Application.Common.Interfaces;
using GymFlow.Domain.Entities;
using GymFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GymFlow.Application.Bookings;

public class BookingService(IApplicationDbContext db, ICurrentUser currentUser, TimeProvider timeProvider)
{
    private const int HistoryLimit = 100;

    public async Task<BookingDto> BookAsync(Guid sessionId, CancellationToken ct)
    {
        var userId = currentUser.RequireUserId();

        var bookingId = await db.WithConcurrencyRetryAsync(async () =>
        {
            var session = await FindSessionAsync(sessionId, ct);

            if (await db.Bookings.AnyAsync(b => b.SessionId == sessionId && b.UserId == userId
                    && (b.Status == BookingStatus.Booked || b.Status == BookingStatus.Waitlisted), ct))
                throw new ConflictException("already_booked", "You already have a spot or a waitlist place in this session.");

            var booking = session.Book(userId, timeProvider.GetUtcNow());
            db.Bookings.Add(booking);
            await db.SaveChangesAsync(ct);
            return booking.Id;
        });

        return await GetDtoAsync(bookingId, ct);
    }

    public async Task<BookingDto> CancelAsync(Guid sessionId, CancellationToken ct)
    {
        var userId = currentUser.RequireUserId();

        var bookingId = await db.WithConcurrencyRetryAsync(async () =>
        {
            var session = await FindSessionAsync(sessionId, ct);
            var bookings = await db.Bookings
                .Where(b => b.SessionId == sessionId
                    && (b.Status == BookingStatus.Booked || b.Status == BookingStatus.Waitlisted))
                .ToListAsync(ct);

            var mine = bookings.SingleOrDefault(b => b.UserId == userId)
                ?? throw new NotFoundException("booking_not_found", "You don't have a booking for this session.");

            session.CancelBooking(mine, bookings.Where(b => b.Status == BookingStatus.Waitlisted), timeProvider.GetUtcNow());
            await db.SaveChangesAsync(ct);
            return mine.Id;
        });

        return await GetDtoAsync(bookingId, ct);
    }

    public async Task<IReadOnlyList<BookingDto>> GetMineAsync(BookingScope scope, CancellationToken ct)
    {
        var userId = currentUser.RequireUserId();
        var now = timeProvider.GetUtcNow();
        var query = db.Bookings.Where(b => b.UserId == userId);

        query = scope == BookingScope.Upcoming
            ? query
                .Where(b => b.Session!.EndsAt >= now
                    && (b.Status == BookingStatus.Booked || b.Status == BookingStatus.Waitlisted))
                .OrderBy(b => b.Session!.StartsAt)
            : query
                .Where(b => b.Session!.EndsAt < now
                    || !(b.Status == BookingStatus.Booked || b.Status == BookingStatus.Waitlisted))
                .OrderByDescending(b => b.Session!.StartsAt)
                .Take(HistoryLimit);

        return await Project(query).ToListAsync(ct);
    }

    private async Task<BookingDto> GetDtoAsync(Guid bookingId, CancellationToken ct) =>
        await Project(db.Bookings.Where(b => b.Id == bookingId)).SingleAsync(ct);

    private IQueryable<BookingDto> Project(IQueryable<Booking> query) =>
        query.AsNoTracking().Select(b => new BookingDto(
            b.Id,
            b.SessionId,
            b.Session!.GymClass!.Name,
            b.Session.Trainer != null ? b.Session.Trainer.FullName : null,
            b.Session.StartsAt,
            b.Session.EndsAt,
            b.Status,
            b.Status == BookingStatus.Waitlisted
                ? db.Bookings.Count(o =>
                    o.SessionId == b.SessionId && o.Status == BookingStatus.Waitlisted && o.CreatedAt <= b.CreatedAt)
                : null,
            b.Session.IsCancelled,
            b.CreatedAt));

    private async Task<ClassSession> FindSessionAsync(Guid sessionId, CancellationToken ct) =>
        await db.ClassSessions.SingleOrDefaultAsync(s => s.Id == sessionId, ct)
        ?? throw new NotFoundException("session_not_found", "Session not found.");
}
