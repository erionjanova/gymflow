using GymFlow.Domain.Entities;
using GymFlow.Infrastructure.Identity;
using GymFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GymFlow.IntegrationTests;

[Collection(PostgresCollection.Name)]
public class DemoResetTests(PostgresFixture fixture)
{
    private const string VisitorEmail = "visitor@test.local";
    private readonly string _database = $"demo_{Guid.NewGuid():N}";

    private async Task InitializeAsync()
    {
        await using var db = fixture.CreateContextForDatabase(_database);
        await new DatabaseInitializer(db, new PasswordHasher(), TimeProvider.System,
            Options.Create(new SeedOptions
            {
                AdminEmail = "admin@demo.test", AdminPassword = "Admin12345", DemoGym = true, DemoResetHours = 24
            }),
            NullLogger<DatabaseInitializer>.Instance).InitializeAsync();
    }

    private async Task SeededHoursAgoAsync(int hours)
    {
        await using var db = fixture.CreateContextForDatabase(_database);
        await db.Users.Where(u => u.IsPlatformAdmin)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.CreatedAt, DateTimeOffset.UtcNow.AddHours(-hours)));
    }

    private async Task<bool> VisitorExistsAsync()
    {
        await using var db = fixture.CreateContextForDatabase(_database);
        return await db.Users.AnyAsync(u => u.Email == VisitorEmail);
    }

    [Fact]
    public async Task Demo_data_older_than_the_limit_is_wiped_and_seeded_fresh()
    {
        await InitializeAsync();
        await using (var db = fixture.CreateContextForDatabase(_database))
        {
            db.Users.Add(new User { FullName = "Visitor", Email = VisitorEmail, PasswordHash = "x" });
            await db.SaveChangesAsync();
        }

        await SeededHoursAgoAsync(23);
        await InitializeAsync();
        Assert.True(await VisitorExistsAsync());

        await SeededHoursAgoAsync(25);
        await InitializeAsync();
        Assert.False(await VisitorExistsAsync());

        await using (var db = fixture.CreateContextForDatabase(_database))
        {
            var admin = await db.Users.SingleAsync(u => u.IsPlatformAdmin);
            Assert.True(admin.CreatedAt > DateTimeOffset.UtcNow.AddMinutes(-5));
            Assert.True(await db.Tenants.AnyAsync(t => t.Slug == "powergym"));
            Assert.True(await db.ClassSessions.IgnoreQueryFilters().AnyAsync());
            Assert.True(await db.MemberSubscriptions.IgnoreQueryFilters().AnyAsync());
            await db.Database.EnsureDeletedAsync();
        }
    }
}
