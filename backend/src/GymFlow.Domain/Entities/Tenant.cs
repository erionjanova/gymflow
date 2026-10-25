using GymFlow.Domain.Common;

namespace GymFlow.Domain.Entities;

public class Tenant : BaseEntity
{
    public const string DefaultBrandColor = "#65a30d";
    public const string DefaultTimeZone = "Europe/Tirane";

    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string BrandColor { get; set; } = DefaultBrandColor;
    public string Currency { get; set; } = "EUR";
    public string TimeZone { get; set; } = DefaultTimeZone;
    public bool IsSuspended { get; set; }
    public bool RequireActiveMembership { get; set; }

    public ICollection<TenantUser> Users { get; set; } = new List<TenantUser>();
    public ICollection<MembershipPlan> Plans { get; set; } = new List<MembershipPlan>();
}
