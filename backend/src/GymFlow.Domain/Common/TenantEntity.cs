using GymFlow.Domain.Entities;

namespace GymFlow.Domain.Common;

public abstract class TenantEntity : BaseEntity, ITenantOwned
{
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }
}
