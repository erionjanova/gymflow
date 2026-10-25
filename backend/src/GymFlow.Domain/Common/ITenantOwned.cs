namespace GymFlow.Domain.Common;

public interface ITenantOwned
{
    Guid TenantId { get; set; }
}
