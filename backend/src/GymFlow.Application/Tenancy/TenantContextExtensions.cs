namespace GymFlow.Application.Tenancy;

public static class TenantContextExtensions
{
    public static Guid RequireTenantId(this ITenantContext context) =>
        context.TenantId ?? throw new InvalidOperationException("No gym was resolved for this request.");
}
