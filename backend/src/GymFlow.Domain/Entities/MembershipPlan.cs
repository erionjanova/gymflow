using GymFlow.Domain.Common;
using GymFlow.Domain.Enums;

namespace GymFlow.Domain.Entities;

public class MembershipPlan : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public BillingInterval Interval { get; set; } = BillingInterval.Month;
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}
