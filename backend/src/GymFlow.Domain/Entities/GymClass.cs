using GymFlow.Domain.Common;

namespace GymFlow.Domain.Entities;

public class GymClass : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int DurationMinutes { get; set; } = 60;
    public int DefaultCapacity { get; set; } = 20;
    public bool IsActive { get; set; } = true;

    public ICollection<ClassSession> Sessions { get; set; } = new List<ClassSession>();
}
