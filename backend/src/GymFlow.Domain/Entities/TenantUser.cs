using GymFlow.Domain.Common;
using GymFlow.Domain.Enums;

namespace GymFlow.Domain.Entities;

public class TenantUser : TenantEntity
{
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public TenantRole Role { get; set; } = TenantRole.Member;
}
