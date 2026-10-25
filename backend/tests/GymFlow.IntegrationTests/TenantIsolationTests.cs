using GymFlow.Application.Common.Exceptions;
using GymFlow.Application.Members;
using GymFlow.Application.Plans;
using GymFlow.Application.Tenancy;
using GymFlow.Domain.Entities;
using GymFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GymFlow.IntegrationTests;

[Collection(PostgresCollection.Name)]
public class TenantIsolationTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Queries_only_return_data_of_the_current_gym()
    {
        var gymA = await fixture.SeedGymAsync("Gym A", "A Basic", "A Premium");
        var gymB = await fixture.SeedGymAsync("Gym B", "B Basic");

        await using var db = fixture.CreateContext(gymA.TenantId);
        var plans = await db.MembershipPlans.Select(p => p.Name).ToListAsync();

        Assert.Equal(["A Basic", "A Premium"], plans.Order());
        Assert.DoesNotContain(plans, p => p.StartsWith("B "));
        Assert.True(await db.MembershipPlans.IgnoreQueryFilters().AnyAsync(p => p.TenantId == gymB.TenantId));
    }

    [Fact]
    public async Task Without_a_gym_no_tenant_data_is_visible()
    {
        await fixture.SeedGymAsync("Gym C", "C Basic");

        await using var db = fixture.CreateContext();

        Assert.Empty(await db.MembershipPlans.ToListAsync());
        Assert.Empty(await db.TenantUsers.ToListAsync());
    }

    [Fact]
    public async Task Another_gyms_record_cannot_be_found_by_id()
    {
        var gymA = await fixture.SeedGymAsync("Gym D", "D Basic");
        var gymB = await fixture.SeedGymAsync("Gym E", "E Basic");

        await using var db = fixture.CreateContext(gymA.TenantId);
        var service = new PlanService(db, new UpsertPlanRequestValidator());
        var request = new UpsertPlanRequest("Hacked", null, 0, BillingInterval.Month, true, 0);

        await Assert.ThrowsAsync<NotFoundException>(() => service.UpdateAsync(gymB.PlanIds[0], request, default));
        await Assert.ThrowsAsync<NotFoundException>(() => service.DeleteAsync(gymB.PlanIds[0], default));

        await using var check = fixture.CreateContext(gymB.TenantId);
        Assert.Equal("E Basic", (await check.MembershipPlans.SingleAsync()).Name);
    }

    [Fact]
    public async Task New_records_are_stamped_with_the_current_gym()
    {
        var gym = await fixture.SeedGymAsync("Gym F");

        await using var db = fixture.CreateContext(gym.TenantId);
        var plan = new MembershipPlan { Name = "Stamped", Price = 30 };
        db.MembershipPlans.Add(plan);
        await db.SaveChangesAsync();

        Assert.Equal(gym.TenantId, plan.TenantId);
    }

    [Fact]
    public async Task Writing_to_another_gyms_record_is_blocked()
    {
        var gymA = await fixture.SeedGymAsync("Gym G", "G Basic");
        var gymB = await fixture.SeedGymAsync("Gym H", "H Basic");

        await using var db = fixture.CreateContext(gymA.TenantId);
        var foreignPlan = await db.MembershipPlans.IgnoreQueryFilters().SingleAsync(p => p.Id == gymB.PlanIds[0]);
        foreignPlan.Price = 0;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        Assert.Contains("another gym", ex.Message);
    }

    [Fact]
    public async Task Adding_a_record_for_another_gym_is_blocked()
    {
        var gymA = await fixture.SeedGymAsync("Gym I");
        var gymB = await fixture.SeedGymAsync("Gym J");

        await using var db = fixture.CreateContext(gymA.TenantId);
        db.MembershipPlans.Add(new MembershipPlan { TenantId = gymB.TenantId, Name = "Sneaky", Price = 1 });

        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Tenant_records_cannot_be_created_without_a_gym()
    {
        await using var db = fixture.CreateContext();
        db.MembershipPlans.Add(new MembershipPlan { Name = "Orphan", Price = 1 });

        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Member_list_only_contains_the_current_gyms_people()
    {
        var gymA = await fixture.SeedGymAsync("Gym K");
        var gymB = await fixture.SeedGymAsync("Gym L");

        await using var db = fixture.CreateContext(gymA.TenantId, TenantRole.Owner);
        var service = new MemberService(db, new FakeCurrentUser(gymA.OwnerId));
        var members = await service.GetAsync(null, null, default);

        Assert.Equal(2, members.Count);
        Assert.All(members, m => Assert.Contains(m.UserId, new[] { gymA.OwnerId, gymA.MemberId }));
        await Assert.ThrowsAsync<NotFoundException>(() => service.RemoveAsync(gymB.MemberId, default));
    }

    [Fact]
    public async Task Resolver_sets_the_users_role_for_that_gym_only()
    {
        var gymA = await fixture.SeedGymAsync("Gym M");
        var gymB = await fixture.SeedGymAsync("Gym N");

        string slugA, slugB;
        await using (var lookup = fixture.CreateContext())
        {
            slugA = await lookup.Tenants.Where(t => t.Id == gymA.TenantId).Select(t => t.Slug).SingleAsync();
            slugB = await lookup.Tenants.Where(t => t.Id == gymB.TenantId).Select(t => t.Slug).SingleAsync();
        }

        Assert.Equal(TenantRole.Owner, await ResolveRoleAsync(slugA, gymA.OwnerId));
        Assert.Null(await ResolveRoleAsync(slugB, gymA.OwnerId));
    }

    [Fact]
    public async Task Suspended_gym_is_blocked_for_regular_users()
    {
        var gym = await fixture.SeedGymAsync("Gym O");

        string slug;
        await using (var db = fixture.CreateContext())
        {
            var tenant = await db.Tenants.SingleAsync(t => t.Id == gym.TenantId);
            tenant.IsSuspended = true;
            await db.SaveChangesAsync();
            slug = tenant.Slug;
        }

        await Assert.ThrowsAsync<ForbiddenException>(() => ResolveRoleAsync(slug, gym.MemberId));
    }

    private async Task<TenantRole?> ResolveRoleAsync(string slug, Guid userId)
    {
        var tenantContext = new TenantContext();
        await using var db = fixture.CreateContextFor(tenantContext);
        var resolver = new TenantResolver(db, tenantContext, new FakeCurrentUser(userId));

        await resolver.ResolveAsync(slug, default);
        return tenantContext.Role;
    }
}
