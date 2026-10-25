using GymFlow.Application.Common;
using GymFlow.Application.Common.Interfaces;
using GymFlow.Domain.Entities;
using GymFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GymFlow.Infrastructure.Persistence;

public class SeedOptions
{
    public const string SectionName = "Seed";

    public string AdminEmail { get; set; } = string.Empty;
    public string AdminPassword { get; set; } = string.Empty;
    public string AdminFullName { get; set; } = "Platform Admin";
    public bool DemoGym { get; set; }
    public string DemoOwnerEmail { get; set; } = "owner@powergym.local";
    public string DemoOwnerPassword { get; set; } = "Owner12345";
    public string DemoTrainerEmail { get; set; } = "trainer@powergym.local";
    public string DemoTrainerPassword { get; set; } = "Trainer12345";
    public string DemoMemberEmail { get; set; } = "member@powergym.local";
    public string DemoMemberPassword { get; set; } = "Member12345";
    public int DemoResetHours { get; set; }
}

public class DatabaseInitializer(
    ApplicationDbContext db,
    IPasswordHasher passwordHasher,
    TimeProvider timeProvider,
    IOptions<SeedOptions> seedOptions,
    ILogger<DatabaseInitializer> logger)
{
    private const string DemoSlug = "powergym";

    private readonly SeedOptions _seed = seedOptions.Value;

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await db.Database.MigrateAsync(ct);
        await ResetStaleDemoAsync(ct);

        await SeedPlatformAdminAsync(ct);
        if (_seed.DemoGym)
        {
            await SeedDemoGymAsync(ct);
            await SeedDemoClassesAsync(ct);
            await SeedDemoHistoryAsync(ct);
        }
    }

    private async Task ResetStaleDemoAsync(CancellationToken ct)
    {
        if (!_seed.DemoGym || _seed.DemoResetHours <= 0)
            return;

        var seededAt = await db.Users.Where(u => u.IsPlatformAdmin)
            .Select(u => (DateTimeOffset?)u.CreatedAt)
            .MinAsync(ct);
        if (seededAt is null || timeProvider.GetUtcNow() - seededAt < TimeSpan.FromHours(_seed.DemoResetHours))
            return;

        var tables = db.Model.GetEntityTypes()
            .Where(t => !t.IsOwned() && t.BaseType is null && t.GetTableName() is not null)
            .Select(t => $"\"{t.GetTableName()}\"")
            .Distinct();

        await db.Database.ExecuteSqlRawAsync($"TRUNCATE TABLE {string.Join(", ", tables)} CASCADE", ct);
        db.ChangeTracker.Clear();
        logger.LogInformation("Demo data was seeded at {SeededAt}, older than {Hours}h, so it was reset", seededAt, _seed.DemoResetHours);
    }

    private async Task SeedPlatformAdminAsync(CancellationToken ct)
    {
        if (await db.Users.AnyAsync(u => u.IsPlatformAdmin, ct))
            return;

        if (string.IsNullOrWhiteSpace(_seed.AdminEmail) || string.IsNullOrWhiteSpace(_seed.AdminPassword))
        {
            logger.LogWarning("No platform admin exists and Seed:AdminEmail / Seed:AdminPassword are not configured.");
            return;
        }

        var admin = CreateUser(_seed.AdminFullName, _seed.AdminEmail, _seed.AdminPassword);
        admin.IsPlatformAdmin = true;

        db.Users.Add(admin);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Created platform admin {Email}", admin.Email);
    }

    private async Task SeedDemoGymAsync(CancellationToken ct)
    {
        if (await db.Tenants.AnyAsync(t => t.Slug == DemoSlug, ct))
            return;

        var ownerEmail = _seed.DemoOwnerEmail.Trim().ToLowerInvariant();
        var owner = await db.Users.SingleOrDefaultAsync(u => u.Email == ownerEmail, ct)
            ?? CreateUser("Driton Berisha", ownerEmail, _seed.DemoOwnerPassword);

        var gym = new Tenant
        {
            Name = "Power Gym",
            Slug = DemoSlug,
            Description = "Strength, conditioning and group classes in the heart of Prishtina.",
            Address = "Rruga Agim Ramadani 15, Prishtinë",
            Phone = "+383 44 111 222",
            BrandColor = "#dc2626"
        };

        gym.Users.Add(new TenantUser { TenantId = gym.Id, User = owner, Role = TenantRole.Owner });
        gym.Plans.Add(new MembershipPlan
        {
            TenantId = gym.Id, Name = "Basic", Description = "Gym floor access, 6:00–22:00.",
            Price = 25, SortOrder = 1
        });
        gym.Plans.Add(new MembershipPlan
        {
            TenantId = gym.Id, Name = "Premium", Description = "Gym floor plus unlimited group classes.",
            Price = 45, SortOrder = 2
        });
        gym.Plans.Add(new MembershipPlan
        {
            TenantId = gym.Id, Name = "Annual", Description = "Premium for a full year, two months free.",
            Price = 450, Interval = BillingInterval.Year, SortOrder = 3
        });

        db.Tenants.Add(gym);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Created demo gym {Slug} owned by {Email}", gym.Slug, owner.Email);
    }

    private async Task SeedDemoClassesAsync(CancellationToken ct)
    {
        var gym = await db.Tenants.SingleOrDefaultAsync(t => t.Slug == DemoSlug, ct);
        if (gym is null || await db.GymClasses.IgnoreQueryFilters().AnyAsync(c => c.TenantId == gym.Id, ct))
            return;

        var trainer = await FindOrCreateDemoUserAsync("Elira Shala", _seed.DemoTrainerEmail, _seed.DemoTrainerPassword, ct);
        var member = await FindOrCreateDemoUserAsync("Valon Morina", _seed.DemoMemberEmail, _seed.DemoMemberPassword, ct);
        await EnsureMembershipAsync(gym, trainer, TenantRole.Trainer, ct);
        await EnsureMembershipAsync(gym, member, TenantRole.Member, ct);

        var yoga = new GymClass
        {
            TenantId = gym.Id, Name = "Yoga Flow", Description = "Mobility and breathing for all levels.",
            DurationMinutes = 60, DefaultCapacity = 16
        };
        var hiit = new GymClass
        {
            TenantId = gym.Id, Name = "HIIT", Description = "45 minutes of intervals. Bring water.",
            DurationMinutes = 45, DefaultCapacity = 12
        };
        var spin = new GymClass
        {
            TenantId = gym.Id, Name = "Spin", Description = "Indoor cycling with music.",
            DurationMinutes = 50, DefaultCapacity = 20
        };
        db.GymClasses.AddRange(yoga, hiit, spin);

        var timeZone = GymTime.FindTimeZone(gym.TimeZone);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), timeZone).DateTime);
        var until = today.AddDays(27);
        var now = timeProvider.GetUtcNow();

        AddWeekly(yoga, trainer, [DayOfWeek.Monday, DayOfWeek.Wednesday], new TimeOnly(18, 0));
        AddWeekly(hiit, trainer, [DayOfWeek.Tuesday, DayOfWeek.Thursday], new TimeOnly(19, 0));
        AddWeekly(spin, null, [DayOfWeek.Saturday], new TimeOnly(10, 0));

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Created demo classes and sessions for {Slug}", gym.Slug);

        void AddWeekly(GymClass gymClass, User? sessionTrainer, DayOfWeek[] days, TimeOnly time)
        {
            foreach (var startsAt in GymTime.WeeklySlots(today, until, days, time, timeZone).Where(s => s > now))
            {
                db.ClassSessions.Add(new ClassSession
                {
                    TenantId = gym.Id,
                    GymClass = gymClass,
                    Trainer = sessionTrainer,
                    StartsAt = startsAt,
                    EndsAt = startsAt.AddMinutes(gymClass.DurationMinutes),
                    Capacity = gymClass.DefaultCapacity
                });
            }
        }
    }

    private static readonly string[] DemoMemberNames =
    [
        "Arta Hoxha", "Besnik Gashi", "Dafina Berisha", "Endrit Kelmendi", "Fjolla Rexhepi", "Granit Krasniqi",
        "Jeta Morina", "Kushtrim Shala", "Liridona Bytyqi", "Mentor Zeka", "Njomza Ahmeti", "Rron Halili"
    ];

    private async Task SeedDemoHistoryAsync(CancellationToken ct)
    {
        var gym = await db.Tenants.SingleOrDefaultAsync(t => t.Slug == DemoSlug, ct);
        if (gym is null)
            return;

        var now = timeProvider.GetUtcNow();
        var classes = await db.GymClasses.IgnoreQueryFilters().Where(c => c.TenantId == gym.Id).ToListAsync(ct);
        var plans = await db.MembershipPlans.IgnoreQueryFilters().Where(p => p.TenantId == gym.Id).ToListAsync(ct);
        if (classes.Count == 0 || plans.Count == 0
            || await db.ClassSessions.IgnoreQueryFilters().AnyAsync(s => s.TenantId == gym.Id && s.StartsAt < now.AddDays(-1), ct))
            return;

        var random = new Random(2026);
        var trainer = await db.Users.SingleOrDefaultAsync(u => u.Email == _seed.DemoTrainerEmail.Trim().ToLowerInvariant(), ct);
        var members = new List<(User User, DateTimeOffset JoinedAt)>();

        for (var i = 0; i < DemoMemberNames.Length; i++)
        {
            var name = DemoMemberNames[i];
            var email = $"{name.Replace(' ', '.').ToLowerInvariant()}@powergym.local";
            if (await db.Users.AnyAsync(u => u.Email == email, ct))
                continue;

            var joinedAt = now.AddDays(-170 + i * 13 + random.Next(0, 5));
            var user = CreateUser(name, email, Guid.NewGuid().ToString("N"));
            user.CreatedAt = joinedAt;
            db.Users.Add(user);
            db.TenantUsers.Add(new TenantUser { TenantId = gym.Id, User = user, Role = TenantRole.Member, CreatedAt = joinedAt });
            members.Add((user, joinedAt));

            if (i % 4 == 3)
                continue;

            var plan = plans[i % plans.Count];
            var startedAt = joinedAt.AddDays(1);
            var cancelled = i is 1 or 6;
            var subscription = new MemberSubscription
            {
                TenantId = gym.Id,
                User = user,
                PlanId = plan.Id,
                StripeSubscriptionId = $"sub_demo_{gym.Slug}_{i}",
                Status = cancelled ? SubscriptionStatus.Canceled : SubscriptionStatus.Active,
                CreatedAt = startedAt,
                LastEventAt = startedAt
            };
            if (cancelled)
            {
                subscription.CanceledAt = startedAt.AddDays(55 + random.Next(0, 20));
                subscription.CurrentPeriodEnd = subscription.CanceledAt;
            }
            else
            {
                subscription.CurrentPeriodEnd = plan.Interval == BillingInterval.Year
                    ? startedAt.AddYears(1)
                    : now.AddDays(random.Next(2, 29));
            }
            db.MemberSubscriptions.Add(subscription);
        }

        var timeZone = GymTime.FindTimeZone(gym.TimeZone);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, timeZone).DateTime);
        var firstDay = today.AddDays(-56);
        var schedule = new (string ClassName, DayOfWeek[] Days, TimeOnly Time, bool WithTrainer)[]
        {
            ("Yoga Flow", [DayOfWeek.Monday, DayOfWeek.Wednesday], new TimeOnly(18, 0), true),
            ("HIIT", [DayOfWeek.Tuesday, DayOfWeek.Thursday], new TimeOnly(19, 0), true),
            ("Spin", [DayOfWeek.Saturday], new TimeOnly(10, 0), false)
        };

        foreach (var (className, days, time, withTrainer) in schedule)
        {
            var gymClass = classes.FirstOrDefault(c => c.Name == className);
            if (gymClass is null)
                continue;

            foreach (var startsAt in GymTime.WeeklySlots(firstDay, today, days, time, timeZone).Where(s => s < now.AddHours(-2)))
            {
                var session = new ClassSession
                {
                    TenantId = gym.Id,
                    GymClass = gymClass,
                    Trainer = withTrainer ? trainer : null,
                    StartsAt = startsAt,
                    EndsAt = startsAt.AddMinutes(gymClass.DurationMinutes),
                    Capacity = gymClass.DefaultCapacity
                };
                db.ClassSessions.Add(session);

                var progress = (startsAt - now.AddDays(-56)).TotalDays / 56;
                var eligible = members.Where(m => m.JoinedAt < startsAt).Select(m => m.User).ToList();
                var wanted = (int)Math.Round(session.Capacity * (0.35 + 0.45 * progress) + random.Next(-2, 3));
                foreach (var user in eligible.OrderBy(_ => random.Next()).Take(Math.Clamp(wanted, 0, Math.Min(eligible.Count, session.Capacity))))
                {
                    var attended = random.NextDouble() < 0.85;
                    db.Bookings.Add(new Booking
                    {
                        TenantId = gym.Id,
                        Session = session,
                        User = user,
                        Status = attended ? BookingStatus.Attended : BookingStatus.NoShow,
                        CreatedAt = startsAt.AddDays(-2),
                        AttendanceMarkedAt = session.EndsAt
                    });
                    session.BookedCount++;
                }
            }
        }

        var upcoming = await db.ClassSessions.IgnoreQueryFilters()
            .Where(s => s.TenantId == gym.Id && !s.IsCancelled && s.StartsAt > now && s.StartsAt < now.AddDays(7))
            .ToListAsync(ct);
        foreach (var session in upcoming)
        {
            var wanted = (int)Math.Round(session.Capacity * (0.4 + random.NextDouble() * 0.4));
            foreach (var (user, _) in members.OrderBy(_ => random.Next()).Take(Math.Min(wanted, members.Count)))
            {
                db.Bookings.Add(new Booking
                {
                    TenantId = gym.Id,
                    SessionId = session.Id,
                    User = user,
                    Status = BookingStatus.Booked,
                    CreatedAt = now.AddHours(-random.Next(1, 72))
                });
                session.BookedCount++;
            }
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Created demo history for {Slug}", gym.Slug);
    }

    private async Task<User> FindOrCreateDemoUserAsync(string fullName, string email, string password, CancellationToken ct)
    {
        var normalized = email.Trim().ToLowerInvariant();
        var user = await db.Users.SingleOrDefaultAsync(u => u.Email == normalized, ct);
        if (user is not null)
            return user;

        user = CreateUser(fullName, normalized, password);
        db.Users.Add(user);
        return user;
    }

    private async Task EnsureMembershipAsync(Tenant gym, User user, TenantRole role, CancellationToken ct)
    {
        var exists = await db.TenantUsers.IgnoreQueryFilters()
            .AnyAsync(tu => tu.TenantId == gym.Id && tu.UserId == user.Id, ct);

        if (!exists)
            db.TenantUsers.Add(new TenantUser { TenantId = gym.Id, User = user, Role = role });
    }

    private User CreateUser(string fullName, string email, string password)
    {
        var user = new User { FullName = fullName, Email = email.Trim().ToLowerInvariant() };
        user.PasswordHash = passwordHasher.Hash(password);
        return user;
    }
}
