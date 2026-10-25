using GymFlow.Api.Auth;
using GymFlow.Application.Plans;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymFlow.Api.Controllers.Gym;

[ApiController]
[Authorize(Policy = Policies.GymOwner)]
[Route("api/g/{slug}/admin/plans")]
public class GymPlansController(PlanService plans) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<PlanDto>> GetAll(string slug, CancellationToken ct) =>
        plans.GetAsync(includeInactive: true, ct);

    [HttpPost]
    public async Task<ActionResult<PlanDto>> Create(string slug, UpsertPlanRequest request, CancellationToken ct)
    {
        var plan = await plans.CreateAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created, plan);
    }

    [HttpPut("{id:guid}")]
    public Task<PlanDto> Update(string slug, Guid id, UpsertPlanRequest request, CancellationToken ct) =>
        plans.UpdateAsync(id, request, ct);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(string slug, Guid id, CancellationToken ct)
    {
        await plans.DeleteAsync(id, ct);
        return NoContent();
    }
}
