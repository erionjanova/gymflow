using GymFlow.Application.Bookings;
using GymFlow.Application.Common;
using GymFlow.Application.Common.Exceptions;
using GymFlow.Application.Notifications;
using GymFlow.Application.Sessions;
using GymFlow.Application.Tenancy;
using GymFlow.Domain.Enums;
using GymFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GymFlow.IntegrationTests;

[Collection(PostgresCollection.Name)]
public class BookingTests(PostgresFixture fixture)
{
    private readonly CapturingEmailSender _email = new();

    private SessionNotifier Notifier(ApplicationDbContext db) =>
        new(db, _email, Options.Create(new AppOptions { FrontendUrl = "http://app.test" }));

    private async Task<string> EmailOfAsync(Guid userId)
    {
        await using var db = fixture.CreateContext();
        return await db.Users.Where(u => u.Id == userId).Select(u => u.Email).SingleAsync();
    }

    private BookingService Bookings(ApplicationDbContext db, Guid tenantId, Guid userId)
    {
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(tenantId, "test");
        tenantContext.SetRole(TenantRole.Member);
        return new BookingService(db, tenantContext, new FakeCurrentUser(userId), TimeProvider.System, Notifier(db));
    }

    private SessionService Sessions(ApplicationDbContext db, Guid userId, TenantRole role, Guid tenantId)
    {
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(tenantId, "test");
        tenantContext.SetRole(role);

        return new SessionService(
            db, tenantContext, new FakeCurrentUser(userId), TimeProvider.System, Notifier(db),
            new CreateSessionRequestValidator(), new ScheduleSessionsRequestValidator(), new UpdateSessionRequestValidator());
    }

    private async Task<BookingDto> BookAsync(Guid tenantId, Guid sessionId, Guid userId)
    {
        await using var db = fixture.CreateContext(tenantId, TenantRole.Member);
        return await Bookings(db, tenantId, userId).BookAsync(sessionId, default);
    }

    private async Task AssertCountsMatchRowsAsync(Guid tenantId, Guid sessionId, int capacity)
    {
        await using var db = fixture.CreateContext(tenantId);
        var session = await db.ClassSessions.SingleAsync(s => s.Id == sessionId);
        var booked = await db.Bookings.CountAsync(b => b.SessionId == sessionId && b.Status == BookingStatus.Booked);
        var waitlisted = await db.Bookings.CountAsync(b => b.SessionId == sessionId && b.Status == BookingStatus.Waitlisted);

        Assert.True(booked <= capacity, $"Overbooked: {booked} booked for {capacity} spots.");
        Assert.Equal(booked, session.BookedCount);
        Assert.Equal(waitlisted, session.WaitlistCount);
    }

    [Fact]
    public async Task Two_people_racing_for_the_last_spot_never_both_get_it()
    {
        var gym = await fixture.SeedGymAsync("Race Gym");
        var users = await fixture.SeedUsersAsync(gym.TenantId, 2);
        var sessionId = await fixture.SeedSessionAsync(gym.TenantId, capacity: 1);

        var results = await Task.WhenAll(users.Select(u => BookAsync(gym.TenantId, sessionId, u)));

        Assert.Single(results, r => r.Status == BookingStatus.Booked);
        Assert.Single(results, r => r.Status == BookingStatus.Waitlisted && r.WaitlistPosition == 1);
        await AssertCountsMatchRowsAsync(gym.TenantId, sessionId, 1);
    }

    [Fact]
    public async Task A_rush_of_bookings_fills_the_class_exactly()
    {
        const int capacity = 5;
        var gym = await fixture.SeedGymAsync("Rush Gym");
        var users = await fixture.SeedUsersAsync(gym.TenantId, 12);
        var sessionId = await fixture.SeedSessionAsync(gym.TenantId, capacity);

        var attempts = await Task.WhenAll(users.Select(async u =>
        {
            try
            {
                return (BookingStatus?)(await BookAsync(gym.TenantId, sessionId, u)).Status;
            }
            catch (ConflictException ex) when (ex.Code == "session_busy")
            {
                return null;
            }
        }));

        Assert.Equal(capacity, attempts.Count(s => s == BookingStatus.Booked));
        await AssertCountsMatchRowsAsync(gym.TenantId, sessionId, capacity);
    }

    [Fact]
    public async Task Same_person_cannot_book_twice()
    {
        var gym = await fixture.SeedGymAsync("Twice Gym");
        var sessionId = await fixture.SeedSessionAsync(gym.TenantId, capacity: 5);

        await BookAsync(gym.TenantId, sessionId, gym.MemberId);
        var ex = await Assert.ThrowsAsync<ConflictException>(() => BookAsync(gym.TenantId, sessionId, gym.MemberId));

        Assert.Equal("already_booked", ex.Code);
    }

    [Fact]
    public async Task Cancelling_promotes_the_next_person_and_they_see_it_in_their_bookings()
    {
        var gym = await fixture.SeedGymAsync("Promote Gym");
        var users = await fixture.SeedUsersAsync(gym.TenantId, 3);
        var sessionId = await fixture.SeedSessionAsync(gym.TenantId, capacity: 1);
        foreach (var user in users)
            await BookAsync(gym.TenantId, sessionId, user);

        await using (var db = fixture.CreateContext(gym.TenantId, TenantRole.Member))
        {
            var cancelled = await Bookings(db, gym.TenantId, users[0]).CancelAsync(sessionId, default);
            Assert.Equal(BookingStatus.Cancelled, cancelled.Status);
        }

        await using (var db = fixture.CreateContext(gym.TenantId, TenantRole.Member))
        {
            var second = await Bookings(db, gym.TenantId, users[1]).GetMineAsync(BookingScope.Upcoming, default);
            var third = await Bookings(db, gym.TenantId, users[2]).GetMineAsync(BookingScope.Upcoming, default);
            var firstHistory = await Bookings(db, gym.TenantId, users[0]).GetMineAsync(BookingScope.Past, default);

            Assert.Equal(BookingStatus.Booked, Assert.Single(second).Status);
            Assert.Equal(1, Assert.Single(third).WaitlistPosition);
            Assert.Equal(BookingStatus.Cancelled, Assert.Single(firstHistory).Status);
        }

        var email = Assert.Single(_email.Sent);
        Assert.Equal(await EmailOfAsync(users[1]), email.To);
        Assert.StartsWith("Keni një vend", email.Subject);
        Assert.Contains("http://app.test/g/", email.HtmlBody);

        await AssertCountsMatchRowsAsync(gym.TenantId, sessionId, 1);
    }

    [Fact]
    public async Task Moving_a_session_emails_everyone_and_promotes_into_new_spots()
    {
        var gym = await fixture.SeedGymAsync("Move Gym");
        var users = await fixture.SeedUsersAsync(gym.TenantId, 3);
        var sessionId = await fixture.SeedSessionAsync(gym.TenantId, capacity: 2);
        foreach (var user in users)
            await BookAsync(gym.TenantId, sessionId, user);

        await using (var db = fixture.CreateContext(gym.TenantId, TenantRole.Owner))
        {
            var session = await db.ClassSessions.AsNoTracking().SingleAsync(s => s.Id == sessionId);
            await Sessions(db, gym.OwnerId, TenantRole.Owner, gym.TenantId)
                .UpdateAsync(sessionId, new UpdateSessionRequest(null, session.StartsAt.AddDays(1), 3), default);
        }

        var promoted = Assert.Single(_email.Sent, e => e.Subject.StartsWith("Keni një vend"));
        Assert.Equal(await EmailOfAsync(users[2]), promoted.To);

        var moved = _email.Sent.Where(e => e.Subject.Contains("u zhvendos")).Select(e => e.To).Order().ToList();
        Assert.Equal((await Task.WhenAll(users.Take(2).Select(EmailOfAsync))).Order().ToList(), moved);
    }

    [Fact]
    public async Task Owner_cancelling_a_session_cancels_all_bookings()
    {
        var gym = await fixture.SeedGymAsync("Cancel Gym");
        var users = await fixture.SeedUsersAsync(gym.TenantId, 3);
        var sessionId = await fixture.SeedSessionAsync(gym.TenantId, capacity: 2);
        foreach (var user in users)
            await BookAsync(gym.TenantId, sessionId, user);

        await using (var db = fixture.CreateContext(gym.TenantId, TenantRole.Owner))
            await Sessions(db, gym.OwnerId, TenantRole.Owner, gym.TenantId).CancelAsync(sessionId, default);

        await using var check = fixture.CreateContext(gym.TenantId);
        Assert.All(await check.Bookings.Where(b => b.SessionId == sessionId).ToListAsync(),
            b => Assert.Equal(BookingStatus.Cancelled, b.Status));
        Assert.True((await check.ClassSessions.SingleAsync(s => s.Id == sessionId)).IsCancelled);
        Assert.Equal(3, _email.Sent.Count(e => e.Subject.EndsWith("u anulua")));
        await Assert.ThrowsAsync<GymFlow.Domain.Exceptions.DomainException>(
            () => BookAsync(gym.TenantId, sessionId, gym.MemberId));
    }

    [Fact]
    public async Task Trainers_only_manage_their_own_sessions()
    {
        var gym = await fixture.SeedGymAsync("Trainer Gym");
        var trainers = await fixture.SeedUsersAsync(gym.TenantId, 2, TenantRole.Trainer);
        var sessionId = await fixture.SeedSessionAsync(gym.TenantId, 5, trainerId: trainers[0], startsIn: TimeSpan.FromMinutes(10));
        await BookAsync(gym.TenantId, sessionId, gym.MemberId);

        await using var db = fixture.CreateContext(gym.TenantId, TenantRole.Trainer);

        var roster = await Sessions(db, trainers[0], TenantRole.Trainer, gym.TenantId).GetRosterAsync(sessionId, default);
        Assert.Equal(gym.MemberId, Assert.Single(roster).UserId);

        var marked = await Sessions(db, trainers[0], TenantRole.Trainer, gym.TenantId)
            .MarkAttendanceAsync(sessionId, gym.MemberId, attended: true, default);
        Assert.Equal(BookingStatus.Attended, marked.Status);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            Sessions(db, trainers[1], TenantRole.Trainer, gym.TenantId).GetRosterAsync(sessionId, default));
    }

    [Fact]
    public async Task Bookings_from_another_gym_are_invisible()
    {
        var gymA = await fixture.SeedGymAsync("Book A");
        var gymB = await fixture.SeedGymAsync("Book B");
        var sessionB = await fixture.SeedSessionAsync(gymB.TenantId, capacity: 5);

        await Assert.ThrowsAsync<NotFoundException>(() => BookAsync(gymA.TenantId, sessionB, gymA.MemberId));
    }
}
