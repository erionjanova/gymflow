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
    string Currency,
    string TimeZone)
{
    public static GymDto From(Tenant t) =>
        new(t.Id, t.Name, t.Slug, t.Description, t.Address, t.Phone, t.BrandColor, t.Currency, t.TimeZone);
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
    string Currency,
    string TimeZone);

public record GymMembershipDto(GymDto Gym, TenantRole? Role);
