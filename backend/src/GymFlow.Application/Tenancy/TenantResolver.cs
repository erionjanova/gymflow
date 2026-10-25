using GymFlow.Application.Common.Exceptions;
using GymFlow.Application.Common.Interfaces;
using GymFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GymFlow.Application.Tenancy;

public class TenantResolver(IApplicationDbContext db, TenantContext tenantContext, ICurrentUser currentUser)
{
    public async Task ResolveAsync(string slug, CancellationToken ct)
    {
        var normalized = slug.Trim().ToLowerInvariant();

        var tenant = await db.Tenants.AsNoTracking()
            .Where(t => t.Slug == normalized)
            .Select(t => new { t.Id, t.Slug, t.IsSuspended })
            .SingleOrDefaultAsync(ct)
            ?? throw new NotFoundException("gym_not_found", "Gym not found.");

        if (tenant.IsSuspended && !currentUser.IsPlatformAdmin)
            throw new ForbiddenException("gym_suspended", "This gym is currently suspended.");

        tenantContext.SetTenant(tenant.Id, tenant.Slug);

        if (currentUser.UserId is not { } userId)
            return;

        var role = await db.TenantUsers.AsNoTracking()
            .Where(tu => tu.UserId == userId)
            .Select(tu => (TenantRole?)tu.Role)
            .SingleOrDefaultAsync(ct);

        tenantContext.SetRole(role);
    }
}
