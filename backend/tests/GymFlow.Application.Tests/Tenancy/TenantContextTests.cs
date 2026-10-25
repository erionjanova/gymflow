using GymFlow.Application.Tenancy;
using GymFlow.Domain.Enums;

namespace GymFlow.Application.Tests.Tenancy;

public class TenantContextTests
{
    [Fact]
    public void SetTenant_stores_tenant_and_role()
    {
        var context = new TenantContext();
        var tenantId = Guid.NewGuid();

        context.SetTenant(tenantId, "powergym");
        context.SetRole(TenantRole.Trainer);

        Assert.Equal(tenantId, context.TenantId);
        Assert.Equal("powergym", context.Slug);
        Assert.Equal(TenantRole.Trainer, context.Role);
    }

    [Fact]
    public void SetTenant_cannot_switch_to_a_different_tenant()
    {
        var context = new TenantContext();
        context.SetTenant(Guid.NewGuid(), "first");

        Assert.Throws<InvalidOperationException>(() => context.SetTenant(Guid.NewGuid(), "second"));
    }

    [Fact]
    public void RequireTenantId_throws_when_no_tenant_resolved()
    {
        var context = new TenantContext();

        Assert.Throws<InvalidOperationException>(() => context.RequireTenantId());
    }
}
