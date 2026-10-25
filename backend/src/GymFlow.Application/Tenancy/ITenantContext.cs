using GymFlow.Domain.Enums;

namespace GymFlow.Application.Tenancy;

public interface ITenantContext
{
    Guid? TenantId { get; }
    string? Slug { get; }
    TenantRole? Role { get; }
}

public class TenantContext : ITenantContext
{
    public Guid? TenantId { get; private set; }
    public string? Slug { get; private set; }
    public TenantRole? Role { get; private set; }

    public void SetTenant(Guid tenantId, string slug)
    {
        if (TenantId is not null && TenantId != tenantId)
            throw new InvalidOperationException("The tenant for this request is already set.");

        TenantId = tenantId;
        Slug = slug;
    }

    public void SetRole(TenantRole? role) => Role = role;
}
