using GymFlow.Application.Common.Exceptions;
using GymFlow.Application.Common.Interfaces;
using GymFlow.Domain.Entities;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace GymFlow.Application.Plans;

public class PlanService(IApplicationDbContext db, IValidator<UpsertPlanRequest> validator)
{
    public async Task<IReadOnlyList<PlanDto>> GetAsync(bool includeInactive, CancellationToken ct)
    {
        var query = db.MembershipPlans.AsNoTracking();
        if (!includeInactive)
            query = query.Where(p => p.IsActive);

        var plans = await query.OrderBy(p => p.SortOrder).ThenBy(p => p.Price).ToListAsync(ct);
        return plans.Select(PlanDto.From).ToList();
    }

    public async Task<PlanDto> CreateAsync(UpsertPlanRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);

        var plan = new MembershipPlan();
        Apply(plan, request);
        db.MembershipPlans.Add(plan);
        await db.SaveChangesAsync(ct);

        return PlanDto.From(plan);
    }

    public async Task<PlanDto> UpdateAsync(Guid id, UpsertPlanRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);

        var plan = await FindAsync(id, ct);
        Apply(plan, request);
        await db.SaveChangesAsync(ct);

        return PlanDto.From(plan);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var plan = await FindAsync(id, ct);
        db.MembershipPlans.Remove(plan);
        await db.SaveChangesAsync(ct);
    }

    private async Task<MembershipPlan> FindAsync(Guid id, CancellationToken ct) =>
        await db.MembershipPlans.SingleOrDefaultAsync(p => p.Id == id, ct)
        ?? throw new NotFoundException("plan_not_found", "Membership plan not found.");

    private static void Apply(MembershipPlan plan, UpsertPlanRequest r)
    {
        plan.Name = r.Name.Trim();
        plan.Description = string.IsNullOrWhiteSpace(r.Description) ? null : r.Description.Trim();
        plan.Price = r.Price;
        plan.Interval = r.Interval;
        plan.IsActive = r.IsActive;
        plan.SortOrder = r.SortOrder;
    }
}
