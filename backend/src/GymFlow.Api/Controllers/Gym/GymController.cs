using GymFlow.Api.Auth;
using GymFlow.Application.Gyms;
using GymFlow.Application.Plans;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymFlow.Api.Controllers.Gym;

[ApiController]
[Route("api/g/{slug}")]
public class GymController(GymService gyms, PlanService plans) : ControllerBase
{
    [HttpGet]
    public Task<GymMembershipDto> Get(string slug, CancellationToken ct) => gyms.GetCurrentAsync(ct);

    [HttpGet("plans")]
    public Task<IReadOnlyList<PlanDto>> Plans(string slug, CancellationToken ct) =>
        plans.GetAsync(includeInactive: false, ct);

    [Authorize]
    [HttpPost("join")]
    public Task<GymMembershipDto> Join(string slug, CancellationToken ct) => gyms.JoinAsync(ct);

    [Authorize(Policy = Policies.GymOwner)]
    [HttpPut("settings")]
    public Task<GymDto> UpdateSettings(string slug, UpdateGymRequest request, CancellationToken ct) =>
        gyms.UpdateAsync(request, ct);
}
