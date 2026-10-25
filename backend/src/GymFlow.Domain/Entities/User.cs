using GymFlow.Domain.Common;

namespace GymFlow.Domain.Entities;

public class User : BaseEntity
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public string PreferredLanguage { get; set; } = "sq";
    public bool IsPlatformAdmin { get; set; }

    public ICollection<TenantUser> Tenants { get; set; } = new List<TenantUser>();
}
