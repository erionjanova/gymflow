using GymFlow.Domain.Entities;
using GymFlow.Domain.Enums;

namespace GymFlow.Application.Gyms;

public record GymDto(
    Guid Id,
    string Name,
    string Slug,
    string? Description,
    string? Address,
    string? Phone,
    string BrandColor,
    string Currency)
{
    public static GymDto From(Tenant t) =>
        new(t.Id, t.Name, t.Slug, t.Description, t.Address, t.Phone, t.BrandColor, t.Currency);
}

public record CreateGymRequest(
    string Name,
    string? Slug,
    string? Description,
    string? Address,
    string? Phone,
    string Currency);

public record UpdateGymRequest(
    string Name,
    string? Description,
    string? Address,
    string? Phone,
    string BrandColor,
    string Currency);

public record GymMembershipDto(GymDto Gym, TenantRole? Role);
