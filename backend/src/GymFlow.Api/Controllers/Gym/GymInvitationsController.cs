using GymFlow.Api.Auth;
using GymFlow.Application.Invitations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymFlow.Api.Controllers.Gym;

[ApiController]
[Authorize(Policy = Policies.GymOwner)]
[Route("api/g/{slug}/admin/invitations")]
public class GymInvitationsController(InvitationService invitations) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<InvitationDto>> GetAll(string slug, CancellationToken ct) => invitations.GetAsync(ct);

    [HttpPost]
    public async Task<ActionResult<InvitationDto>> Create(string slug, CreateInvitationRequest request, CancellationToken ct)
    {
        var invitation = await invitations.CreateAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created, invitation);
    }

    [HttpPost("{id:guid}/resend")]
    public Task<InvitationDto> Resend(string slug, Guid id, CancellationToken ct) => invitations.ResendAsync(id, ct);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Revoke(string slug, Guid id, CancellationToken ct)
    {
        await invitations.RevokeAsync(id, ct);
        return NoContent();
    }
}
