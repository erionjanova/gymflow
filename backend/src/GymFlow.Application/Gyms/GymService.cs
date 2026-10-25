using GymFlow.Application.Common;
using GymFlow.Application.Common.Exceptions;
using GymFlow.Application.Common.Interfaces;
using GymFlow.Application.Tenancy;
using GymFlow.Domain.Entities;
using GymFlow.Domain.Enums;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace GymFlow.Application.Gyms;

public class GymService(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    ITenantContext tenantContext,
    IValidator<CreateGymRequest> createValidator,
    IValidator<UpdateGymRequest> updateValidator)
{
    public async Task<GymMembershipDto> CreateAsync(CreateGymRequest request, CancellationToken ct)
    {
        await createValidator.ValidateAndThrowAsync(request, ct);
        var userId = currentUser.RequireUserId();

        var slug = string.IsNullOrWhiteSpace(request.Slug) ? SlugRules.Normalize(request.Name) : request.Slug.Trim();
        if (!SlugRules.IsValid(slug))
            throw new ValidationException([new FluentValidation.Results.ValidationFailure(nameof(request.Slug), "Choose a different address for your gym.")]);

        if (await db.Tenants.AnyAsync(t => t.Slug == slug, ct))
            throw new ConflictException("slug_taken", "This gym address is already taken.");

        var tenant = new Tenant
        {
            Name = request.Name.Trim(),
            Slug = slug,
            Description = TrimToNull(request.Description),
            Address = TrimToNull(request.Address),
            Phone = TrimToNull(request.Phone),
            Currency = request.Currency
        };

        db.Tenants.Add(tenant);
        db.TenantUsers.Add(new TenantUser { TenantId = tenant.Id, UserId = userId, Role = TenantRole.Owner });
        await db.SaveChangesAsync(ct);

        return new GymMembershipDto(GymDto.From(tenant), TenantRole.Owner);
    }

    public async Task<GymMembershipDto> GetCurrentAsync(CancellationToken ct)
    {
        var tenant = await FindCurrentAsync(ct);
        return new GymMembershipDto(GymDto.From(tenant), tenantContext.Role);
    }

    public async Task<GymDto> UpdateAsync(UpdateGymRequest request, CancellationToken ct)
    {
        await updateValidator.ValidateAndThrowAsync(request, ct);

        var tenant = await FindCurrentAsync(ct);
        tenant.Name = request.Name.Trim();
        tenant.Description = TrimToNull(request.Description);
        tenant.Address = TrimToNull(request.Address);
        tenant.Phone = TrimToNull(request.Phone);
        tenant.BrandColor = request.BrandColor.ToLowerInvariant();
        tenant.Currency = request.Currency;
        tenant.TimeZone = request.TimeZone;
        await db.SaveChangesAsync(ct);

        return GymDto.From(tenant);
    }

    public async Task<GymMembershipDto> JoinAsync(CancellationToken ct)
    {
        var userId = currentUser.RequireUserId();
        var tenant = await FindCurrentAsync(ct);

        var existing = await db.TenantUsers.SingleOrDefaultAsync(tu => tu.UserId == userId, ct);
        if (existing is not null)
            return new GymMembershipDto(GymDto.From(tenant), existing.Role);

        db.TenantUsers.Add(new TenantUser { UserId = userId, Role = TenantRole.Member });
        await db.SaveChangesAsync(ct);

        return new GymMembershipDto(GymDto.From(tenant), TenantRole.Member);
    }

    private async Task<Tenant> FindCurrentAsync(CancellationToken ct)
    {
        var tenantId = tenantContext.RequireTenantId();
        return await db.Tenants.SingleOrDefaultAsync(t => t.Id == tenantId, ct)
            ?? throw new NotFoundException("gym_not_found", "Gym not found.");
    }

    private static string? TrimToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
