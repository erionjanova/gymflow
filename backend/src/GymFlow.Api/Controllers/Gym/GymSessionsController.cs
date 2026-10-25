using GymFlow.Api.Auth;
using GymFlow.Application.Sessions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymFlow.Api.Controllers.Gym;

[ApiController]
[Route("api/g/{slug}/admin/sessions")]
public class GymSessionsController(SessionService sessions) : ControllerBase
{
    [Authorize(Policy = Policies.GymStaff)]
    [HttpGet]
    public Task<IReadOnlyList<SessionDto>> GetAll(
        string slug,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] Guid? trainerId,
        CancellationToken ct) =>
        sessions.GetForStaffAsync(from, to, trainerId, ct);

    [Authorize(Policy = Policies.GymOwner)]
    [HttpPost]
    public async Task<ActionResult<SessionDto>> Create(string slug, CreateSessionRequest request, CancellationToken ct)
    {
        var session = await sessions.CreateAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created, session);
    }

    [Authorize(Policy = Policies.GymOwner)]
    [HttpPost("schedule")]
    public async Task<ActionResult<ScheduleSessionsResult>> Schedule(
        string slug, ScheduleSessionsRequest request, CancellationToken ct)
    {
        var result = await sessions.ScheduleAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [Authorize(Policy = Policies.GymOwner)]
    [HttpPut("{id:guid}")]
    public Task<SessionDto> Update(string slug, Guid id, UpdateSessionRequest request, CancellationToken ct) =>
        sessions.UpdateAsync(id, request, ct);

    [Authorize(Policy = Policies.GymOwner)]
    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(string slug, Guid id, CancellationToken ct)
    {
        await sessions.CancelAsync(id, ct);
        return NoContent();
    }

    [Authorize(Policy = Policies.GymStaff)]
    [HttpGet("{id:guid}/roster")]
    public Task<IReadOnlyList<RosterEntryDto>> Roster(string slug, Guid id, CancellationToken ct) =>
        sessions.GetRosterAsync(id, ct);

    [Authorize(Policy = Policies.GymStaff)]
    [HttpPut("{id:guid}/attendance/{userId:guid}")]
    public Task<RosterEntryDto> MarkAttendance(
        string slug, Guid id, Guid userId, MarkAttendanceRequest request, CancellationToken ct) =>
        sessions.MarkAttendanceAsync(id, userId, request.Attended, ct);
}
