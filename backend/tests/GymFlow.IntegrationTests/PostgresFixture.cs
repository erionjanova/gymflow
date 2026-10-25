using GymFlow.Application.Common.Interfaces;
using GymFlow.Application.Tenancy;
using GymFlow.Domain.Entities;
using GymFlow.Domain.Enums;
using GymFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace GymFlow.IntegrationTests;

public class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    public ApplicationDbContext CreateContext(Guid? tenantId = null, TenantRole? role = null)
    {
        var tenantContext = new TenantContext();
        if (tenantId is { } id)
        {
            tenantContext.SetTenant(id, "test");
            tenantContext.SetRole(role);
        }

        return CreateContextFor(tenantContext);
    }

    public ApplicationDbContext CreateContextFor(TenantContext tenantContext)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(_container.GetConnectionString())
            .UseSnakeCaseNamingConvention()
            .Options;

        return new ApplicationDbContext(options, tenantContext);
    }

    public async Task<SeededGym> SeedGymAsync(string name, params string[] planNames)
    {
        await using var db = CreateContext();

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var owner = new User { FullName = $"{name} Owner", Email = $"owner-{suffix}@test.local", PasswordHash = "x" };
        var member = new User { FullName = $"{name} Member", Email = $"member-{suffix}@test.local", PasswordHash = "x" };
        var gym = new Tenant { Name = name, Slug = $"gym-{suffix}" };

        gym.Users.Add(new TenantUser { TenantId = gym.Id, User = owner, Role = TenantRole.Owner });
        gym.Users.Add(new TenantUser { TenantId = gym.Id, User = member, Role = TenantRole.Member });

        foreach (var (planName, index) in planNames.Select((p, i) => (p, i)))
            gym.Plans.Add(new MembershipPlan { TenantId = gym.Id, Name = planName, Price = 20 + index, SortOrder = index });

        db.Tenants.Add(gym);
        await db.SaveChangesAsync();

        return new SeededGym(gym.Id, owner.Id, member.Id, gym.Plans.Select(p => p.Id).ToList());
    }

    public async Task<IReadOnlyList<Guid>> SeedUsersAsync(Guid tenantId, int count, TenantRole role = TenantRole.Member)
    {
        await using var db = CreateContext(tenantId);

        var users = Enumerable.Range(0, count)
            .Select(i => new User { FullName = $"{role} {i}", Email = $"{Guid.NewGuid():N}@test.local", PasswordHash = "x" })
            .ToList();

        db.Users.AddRange(users);
        db.TenantUsers.AddRange(users.Select(u => new TenantUser { User = u, Role = role }));
        await db.SaveChangesAsync();

        return users.Select(u => u.Id).ToList();
    }

    public async Task<Guid> SeedSessionAsync(Guid tenantId, int capacity, Guid? trainerId = null, TimeSpan? startsIn = null)
    {
        await using var db = CreateContext(tenantId);

        var gymClass = new GymClass { Name = "Test class", DurationMinutes = 60, DefaultCapacity = capacity };
        var startsAt = DateTimeOffset.UtcNow.Add(startsIn ?? TimeSpan.FromDays(1));
        var session = new ClassSession
        {
            GymClass = gymClass,
            TrainerId = trainerId,
            StartsAt = startsAt,
            EndsAt = startsAt.AddHours(1),
            Capacity = capacity
        };

        db.GymClasses.Add(gymClass);
        db.ClassSessions.Add(session);
        await db.SaveChangesAsync();

        return session.Id;
    }
}

public record SeededGym(Guid TenantId, Guid OwnerId, Guid MemberId, IReadOnlyList<Guid> PlanIds);

public class FakeCurrentUser(Guid? userId, bool isPlatformAdmin = false) : ICurrentUser
{
    public Guid? UserId { get; } = userId;
    public bool IsPlatformAdmin { get; } = isPlatformAdmin;
}

[CollectionDefinition(Name)]
public class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}
