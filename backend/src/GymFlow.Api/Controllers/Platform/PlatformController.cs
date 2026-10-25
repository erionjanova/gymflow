using GymFlow.Api.Auth;
using GymFlow.Application.Platform;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymFlow.Api.Controllers.Platform;

[ApiController]
[Authorize(Policy = Policies.PlatformAdmin)]
[Route("api/platform")]
public class PlatformController(PlatformService platform) : ControllerBase
{
    [HttpGet("stats")]
    public Task<PlatformStatsDto> Stats(CancellationToken ct) => platform.GetStatsAsync(ct);

    [HttpGet("gyms")]
    public Task<IReadOnlyList<PlatformGymDto>> Gyms(CancellationToken ct) => platform.GetGymsAsync(ct);

    [HttpPut("gyms/{id:guid}/suspension")]
    public async Task<IActionResult> SetSuspended(Guid id, SetSuspendedRequest request, CancellationToken ct)
    {
        await platform.SetSuspendedAsync(id, request.IsSuspended, ct);
        return NoContent();
    }
}
