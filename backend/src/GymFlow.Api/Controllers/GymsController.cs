using GymFlow.Application.Gyms;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymFlow.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/gyms")]
public class GymsController(GymService gyms) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<GymMembershipDto>> Create(CreateGymRequest request, CancellationToken ct)
    {
        var gym = await gyms.CreateAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created, gym);
    }
}
