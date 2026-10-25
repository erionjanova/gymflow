using GymFlow.Api.Auth;
using GymFlow.Application.Invitations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GymFlow.Api.Controllers;

[ApiController]
[Route("api/invitations/{token}")]
public class InvitationsController(InvitationService invitations) : ControllerBase
{
    [HttpGet]
    public Task<InvitationPreviewDto> Preview(string token, CancellationToken ct) =>
        invitations.PreviewAsync(token, ct);

    [HttpPost("accept")]
    [EnableRateLimiting(RateLimits.Auth)]
    public Task<AcceptInvitationResponse> Accept(string token, AcceptInvitationRequest request, CancellationToken ct) =>
        invitations.AcceptAsync(token, request, ct);
}
