using GymFlow.Application.Common;
using GymFlow.Application.Common.Exceptions;
using GymFlow.Application.Common.Interfaces;
using GymFlow.Application.Tenancy;
using GymFlow.Domain.Entities;
using GymFlow.Domain.Enums;
using GymFlow.Domain.Exceptions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;

namespace GymFlow.Application.Sessions;

public class SessionService(
    IApplicationDbContext db,
    ITenantContext tenantContext,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    IValidator<CreateSessionRequest> createValidator,
    IValidator<ScheduleSessionsRequest> scheduleValidator,
    IValidator<UpdateSessionRequest> updateValidator)
{
    public Task<IReadOnlyList<SessionDto>> GetScheduleAsync(DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct)
    {
        var (start, end) = ResolveRange(from, to);
        var query = db.ClassSessions
            .Where(s => s.StartsAt >= start && s.StartsAt < end && !s.IsCancelled)
            .OrderBy(s => s.StartsAt);

        return db.ToDtosAsync(query, currentUser.UserId, ct);
    }

    public Task<IReadOnlyList<SessionDto>> GetForStaffAsync(
        DateTimeOffset? from, DateTimeOffset? to, Guid? trainerId, CancellationToken ct)
    {
        if (tenantContext.Role == TenantRole.Trainer)
            trainerId = currentUser.RequireUserId();

        var (start, end) = ResolveRange(from, to);
        var query = db.ClassSessions.Where(s => s.StartsAt >= start && s.StartsAt < end);
        if (trainerId is not null)
            query = query.Where(s => s.TrainerId == trainerId);

        return db.ToDtosAsync(query.OrderBy(s => s.StartsAt), null, ct);
    }

    public async Task<SessionDto> CreateAsync(CreateSessionRequest request, CancellationToken ct)
    {
        await createValidator.ValidateAndThrowAsync(request, ct);

        var gymClass = await FindActiveClassAsync(request.GymClassId, ct);
        await EnsureTrainerAsync(request.TrainerId, ct);

        var startsAt = request.StartsAt.ToUniversalTime();
        if (startsAt <= timeProvider.GetUtcNow())
            throw new DomainException("session_in_past", "A session must start in the future.");

        var session = NewSession(gymClass, request.TrainerId, startsAt, request.Capacity);
        db.ClassSessions.Add(session);
        await db.SaveChangesAsync(ct);

        return await GetDtoAsync(session.Id, ct);
    }

    public async Task<ScheduleSessionsResult> ScheduleAsync(ScheduleSessionsRequest request, CancellationToken ct)
    {
        await scheduleValidator.ValidateAndThrowAsync(request, ct);

        var gymClass = await FindActiveClassAsync(request.GymClassId, ct);
        await EnsureTrainerAsync(request.TrainerId, ct);

        var tenantId = tenantContext.RequireTenantId();
        var timeZoneId = await db.Tenants.Where(t => t.Id == tenantId).Select(t => t.TimeZone).SingleAsync(ct);
        var now = timeProvider.GetUtcNow();

        var allSlots = GymTime.WeeklySlots(
            request.FromDate, request.ToDate, request.DaysOfWeek.Distinct().ToList(), request.StartTime,
            GymTime.FindTimeZone(timeZoneId)).ToList();
        var futureSlots = allSlots.Where(s => s > now).ToList();

        if (futureSlots.Count > SessionRules.MaxSessionsPerSchedule)
            throw new ValidationException([new ValidationFailure(nameof(request.ToDate),
                $"That would create {futureSlots.Count} sessions. The limit is {SessionRules.MaxSessionsPerSchedule} at once.")]);

        var existing = (await db.ClassSessions
                .Where(s => s.GymClassId == gymClass.Id && !s.IsCancelled && futureSlots.Contains(s.StartsAt))
                .Select(s => s.StartsAt)
                .ToListAsync(ct))
            .ToHashSet();

        var created = futureSlots
            .Where(slot => !existing.Contains(slot))
            .Select(slot => NewSession(gymClass, request.TrainerId, slot, request.Capacity))
            .ToList();

        db.ClassSessions.AddRange(created);
        await db.SaveChangesAsync(ct);

        var ids = created.Select(s => s.Id).ToList();
        var sessions = await db.ToDtosAsync(
            db.ClassSessions.Where(s => ids.Contains(s.Id)).OrderBy(s => s.StartsAt), null, ct);

        return new ScheduleSessionsResult(created.Count, allSlots.Count - created.Count, sessions);
    }

    public async Task<SessionDto> UpdateAsync(Guid id, UpdateSessionRequest request, CancellationToken ct)
    {
        await updateValidator.ValidateAndThrowAsync(request, ct);
        await EnsureTrainerAsync(request.TrainerId, ct);

        return await db.WithConcurrencyRetryAsync(async () =>
        {
            var session = await FindAsync(id, ct);
            var now = timeProvider.GetUtcNow();

            if (session.IsCancelled)
                throw new DomainException("session_cancelled", "This session was cancelled.");
            if (now >= session.StartsAt)
                throw new DomainException("session_started", "A session that already started can't be changed.");

            var startsAt = request.StartsAt.ToUniversalTime();
            if (startsAt != session.StartsAt)
            {
                if (startsAt <= now)
                    throw new DomainException("session_in_past", "A session must start in the future.");

                var duration = session.EndsAt - session.StartsAt;
                session.StartsAt = startsAt;
                session.EndsAt = startsAt + duration;
            }

            session.TrainerId = request.TrainerId;

            var waitlist = await db.Bookings
                .Where(b => b.SessionId == id && b.Status == BookingStatus.Waitlisted)
                .ToListAsync(ct);
            session.ChangeCapacity(request.Capacity, waitlist, now);

            await db.SaveChangesAsync(ct);
            return await GetDtoAsync(id, ct);
        });
    }

    public async Task CancelAsync(Guid id, CancellationToken ct)
    {
        await db.WithConcurrencyRetryAsync(async () =>
        {
            var session = await FindAsync(id, ct);
            var active = await db.Bookings
                .Where(b => b.SessionId == id && (b.Status == BookingStatus.Booked || b.Status == BookingStatus.Waitlisted))
                .ToListAsync(ct);

            session.Cancel(active, timeProvider.GetUtcNow());
            await db.SaveChangesAsync(ct);
            return true;
        });
    }

    public async Task<IReadOnlyList<RosterEntryDto>> GetRosterAsync(Guid id, CancellationToken ct)
    {
        var session = await db.ClassSessions.AsNoTracking().SingleOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new NotFoundException("session_not_found", "Session not found.");
        EnsureCanManage(session);

        var rows = await db.Bookings.AsNoTracking()
            .Where(b => b.SessionId == id && b.Status != BookingStatus.Cancelled)
            .OrderBy(b => b.Status == BookingStatus.Waitlisted)
            .ThenBy(b => b.CreatedAt)
            .Select(b => new { b.Id, b.UserId, b.User!.FullName, b.User.Email, b.Status, b.CreatedAt })
            .ToListAsync(ct);

        var position = 0;
        return rows
            .Select(r => new RosterEntryDto(
                r.Id, r.UserId, r.FullName, r.Email, r.Status, r.CreatedAt,
                r.Status == BookingStatus.Waitlisted ? ++position : null))
            .ToList();
    }

    public async Task<RosterEntryDto> MarkAttendanceAsync(Guid id, Guid userId, bool attended, CancellationToken ct)
    {
        var session = await db.ClassSessions.AsNoTracking().SingleOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new NotFoundException("session_not_found", "Session not found.");
        EnsureCanManage(session);

        var now = timeProvider.GetUtcNow();
        if (session.IsCancelled)
            throw new DomainException("session_cancelled", "This session was cancelled.");
        if (now < session.StartsAt - SessionRules.AttendanceOpensBefore)
            throw new DomainException("attendance_too_early", "Attendance opens 30 minutes before the session starts.");

        var booking = await db.Bookings.Include(b => b.User)
            .SingleOrDefaultAsync(b => b.SessionId == id && b.UserId == userId
                && (b.Status == BookingStatus.Booked || b.Status == BookingStatus.Attended || b.Status == BookingStatus.NoShow), ct)
            ?? throw new NotFoundException("booking_not_found", "This person has no confirmed booking for the session.");

        booking.MarkAttendance(attended, now);
        await db.SaveChangesAsync(ct);

        return new RosterEntryDto(
            booking.Id, booking.UserId, booking.User!.FullName, booking.User.Email, booking.Status, booking.CreatedAt, null);
    }

    private (DateTimeOffset Start, DateTimeOffset End) ResolveRange(DateTimeOffset? from, DateTimeOffset? to)
    {
        var start = (from ?? timeProvider.GetUtcNow()).ToUniversalTime();
        var end = (to ?? start.AddDays(7)).ToUniversalTime();

        if (end <= start)
            throw new ValidationException([new ValidationFailure("to", "'to' must be after 'from'.")]);
        if (end - start > TimeSpan.FromDays(SessionRules.MaxRangeDays))
            throw new ValidationException([new ValidationFailure("to", $"The range can be at most {SessionRules.MaxRangeDays} days.")]);

        return (start, end);
    }

    private void EnsureCanManage(ClassSession session)
    {
        if (tenantContext.Role == TenantRole.Owner)
            return;

        if (tenantContext.Role == TenantRole.Trainer && session.TrainerId == currentUser.UserId)
            return;

        throw new ForbiddenException("not_your_session", "Only the session's trainer or the gym owner can do this.");
    }

    private async Task EnsureTrainerAsync(Guid? trainerId, CancellationToken ct)
    {
        if (trainerId is null)
            return;

        var isStaff = await db.TenantUsers.AnyAsync(tu =>
            tu.UserId == trainerId && (tu.Role == TenantRole.Trainer || tu.Role == TenantRole.Owner), ct);

        if (!isStaff)
            throw new DomainException("trainer_invalid", "Pick a trainer who works at this gym.");
    }

    private async Task<GymClass> FindActiveClassAsync(Guid id, CancellationToken ct)
    {
        var gymClass = await db.GymClasses.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new NotFoundException("class_not_found", "Class not found.");

        if (!gymClass.IsActive)
            throw new DomainException("class_inactive", "This class is not active.");

        return gymClass;
    }

    private async Task<ClassSession> FindAsync(Guid id, CancellationToken ct) =>
        await db.ClassSessions.SingleOrDefaultAsync(s => s.Id == id, ct)
        ?? throw new NotFoundException("session_not_found", "Session not found.");

    private async Task<SessionDto> GetDtoAsync(Guid id, CancellationToken ct) =>
        (await db.ToDtosAsync(db.ClassSessions.Where(s => s.Id == id), currentUser.UserId, ct)).Single();

    private static ClassSession NewSession(GymClass gymClass, Guid? trainerId, DateTimeOffset startsAt, int? capacity) =>
        new()
        {
            GymClassId = gymClass.Id,
            TrainerId = trainerId,
            StartsAt = startsAt,
            EndsAt = startsAt.AddMinutes(gymClass.DurationMinutes),
            Capacity = capacity ?? gymClass.DefaultCapacity
        };
}
