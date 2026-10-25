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
}

public class DatabaseInitializer(
    ApplicationDbContext db,
    IPasswordHasher passwordHasher,
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
            await SeedDemoGymAsync(ct);
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
            ?? CreateUser("Arben Krasniqi", ownerEmail, _seed.DemoOwnerPassword);

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

    private User CreateUser(string fullName, string email, string password)
    {
        var user = new User { FullName = fullName, Email = email.Trim().ToLowerInvariant() };
        user.PasswordHash = passwordHasher.Hash(password);
        return user;
    }
}
