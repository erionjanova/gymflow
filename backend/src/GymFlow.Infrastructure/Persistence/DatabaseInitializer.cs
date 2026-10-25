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

        await SeedPlatformAdminAsync(ct);
        if (_seed.DemoGym)
        {
            await SeedDemoGymAsync(ct);
            await SeedDemoClassesAsync(ct);
        }
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
