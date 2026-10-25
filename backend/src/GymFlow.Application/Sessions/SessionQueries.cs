using GymFlow.Application.Common.Interfaces;
using GymFlow.Domain.Entities;
using GymFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GymFlow.Application.Sessions;

public static class SessionQueries
{
    public static async Task<IReadOnlyList<SessionDto>> ToDtosAsync(
        this IApplicationDbContext db, IQueryable<ClassSession> query, Guid? userId, CancellationToken ct)
    {
        var rows = await query.AsNoTracking()
            .Select(s => new
            {
                s.Id,
                s.GymClassId,
                ClassName = s.GymClass!.Name,
                s.TrainerId,
                TrainerName = s.Trainer != null ? s.Trainer.FullName : null,
                s.StartsAt,
                s.EndsAt,
                s.Capacity,
                s.BookedCount,
                s.WaitlistCount,
                s.IsCancelled
            })
            .ToListAsync(ct);

        var mine = new Dictionary<Guid, (BookingStatus Status, int? Position)>();
        if (userId is { } uid && rows.Count > 0)
        {
            var ids = rows.Select(r => r.Id).ToList();
            var bookings = await db.Bookings.AsNoTracking()
                .Where(b => b.UserId == uid && ids.Contains(b.SessionId) && b.Status != BookingStatus.Cancelled)
                .Select(b => new
                {
                    b.SessionId,
                    b.Status,
                    Position = b.Status == BookingStatus.Waitlisted
                        ? db.Bookings.Count(o =>
                            o.SessionId == b.SessionId && o.Status == BookingStatus.Waitlisted && o.CreatedAt <= b.CreatedAt)
                        : (int?)null
                })
                .ToListAsync(ct);

            foreach (var b in bookings)
                mine[b.SessionId] = (b.Status, b.Position);
        }

        return rows
            .Select(r =>
            {
                var hasMine = mine.TryGetValue(r.Id, out var my);
                return new SessionDto(
                    r.Id, r.GymClassId, r.ClassName, r.TrainerId, r.TrainerName, r.StartsAt, r.EndsAt,
                    r.Capacity, r.BookedCount, Math.Max(0, r.Capacity - r.BookedCount), r.WaitlistCount, r.IsCancelled,
                    hasMine ? my.Status : null, hasMine ? my.Position : null);
            })
            .ToList();
    }
}
