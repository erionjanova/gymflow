using GymFlow.Application.Common.Exceptions;
using GymFlow.Application.Common.Interfaces;
using GymFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GymFlow.Application.Platform;

public class PlatformService(IApplicationDbContext db)
{
    public async Task<IReadOnlyList<PlatformGymDto>> GetGymsAsync(CancellationToken ct)
    {
        var memberships = db.TenantUsers.IgnoreQueryFilters();

        return await db.Tenants.AsNoTracking()
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new PlatformGymDto(
                t.Id,
                t.Name,
                t.Slug,
                t.IsSuspended,
                t.CreatedAt,
                memberships.Count(tu => tu.TenantId == t.Id && tu.Role == TenantRole.Member),
                memberships
                    .Where(tu => tu.TenantId == t.Id && tu.Role == TenantRole.Owner)
                    .OrderBy(tu => tu.CreatedAt)
                    .Select(tu => tu.User!.Email)
                    .FirstOrDefault()))
            .ToListAsync(ct);
    }

    public async Task<PlatformStatsDto> GetStatsAsync(CancellationToken ct)
    {
        var gyms = await db.Tenants.CountAsync(ct);
        var activeGyms = await db.Tenants.CountAsync(t => !t.IsSuspended, ct);
        var users = await db.Users.CountAsync(ct);
        var memberships = await db.TenantUsers.IgnoreQueryFilters().CountAsync(ct);

        return new PlatformStatsDto(gyms, activeGyms, users, memberships);
    }

    public async Task SetSuspendedAsync(Guid tenantId, bool isSuspended, CancellationToken ct)
    {
        var tenant = await db.Tenants.SingleOrDefaultAsync(t => t.Id == tenantId, ct)
            ?? throw new NotFoundException("gym_not_found", "Gym not found.");

        tenant.IsSuspended = isSuspended;
        await db.SaveChangesAsync(ct);
    }
}
