using GymFlow.Domain.Entities;
using GymFlow.Domain.Enums;

namespace GymFlow.Application.Plans;

public record PlanDto(
    Guid Id,
    string Name,
    string? Description,
    decimal Price,
    BillingInterval Interval,
    bool IsActive,
    int SortOrder)
{
    public static PlanDto From(MembershipPlan p) =>
        new(p.Id, p.Name, p.Description, p.Price, p.Interval, p.IsActive, p.SortOrder);
}

public record UpsertPlanRequest(
    string Name,
    string? Description,
    decimal Price,
    BillingInterval Interval,
    bool IsActive,
    int SortOrder);
