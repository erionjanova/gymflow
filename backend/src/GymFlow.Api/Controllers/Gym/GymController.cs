using GymFlow.Api.Auth;
using GymFlow.Application.Classes;
using GymFlow.Application.Gyms;
using GymFlow.Application.Plans;
using GymFlow.Application.Sessions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymFlow.Api.Controllers.Gym;

[ApiController]
[Route("api/g/{slug}")]
public class GymController(GymService gyms, PlanService plans, ClassService classes, SessionService sessions)
    : ControllerBase
{
    [HttpGet]
    public Task<GymMembershipDto> Get(string slug, CancellationToken ct) => gyms.GetCurrentAsync(ct);

    [HttpGet("plans")]
    public Task<IReadOnlyList<PlanDto>> Plans(string slug, CancellationToken ct) =>
        plans.GetAsync(includeInactive: false, ct);

    [HttpGet("classes")]
    public Task<IReadOnlyList<ClassDto>> Classes(string slug, CancellationToken ct) =>
        classes.GetAsync(includeInactive: false, ct);

    [HttpGet("schedule")]
    public Task<IReadOnlyList<SessionDto>> Schedule(
        string slug, [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to, CancellationToken ct) =>
        sessions.GetScheduleAsync(from, to, ct);

    [Authorize]
    [HttpPost("join")]
    public Task<GymMembershipDto> Join(string slug, CancellationToken ct) => gyms.JoinAsync(ct);

    [Authorize(Policy = Policies.GymOwner)]
    [HttpPut("settings")]
    public Task<GymDto> UpdateSettings(string slug, UpdateGymRequest request, CancellationToken ct) =>
        gyms.UpdateAsync(request, ct);
}
