using GymFlow.Api.Auth;
using GymFlow.Application.Members;
using GymFlow.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymFlow.Api.Controllers.Gym;

[ApiController]
[Route("api/g/{slug}/admin/members")]
public class GymMembersController(MemberService members) : ControllerBase
{
    [Authorize(Policy = Policies.GymStaff)]
    [HttpGet]
    public Task<IReadOnlyList<MemberDto>> GetAll(
        string slug, [FromQuery] TenantRole? role, [FromQuery] string? search, CancellationToken ct) =>
        members.GetAsync(role, search, ct);

    [Authorize(Policy = Policies.GymOwner)]
    [HttpPut("{userId:guid}/role")]
    public Task<MemberDto> ChangeRole(string slug, Guid userId, ChangeRoleRequest request, CancellationToken ct) =>
        members.ChangeRoleAsync(userId, request.Role, ct);

    [Authorize(Policy = Policies.GymOwner)]
    [HttpDelete("{userId:guid}")]
    public async Task<IActionResult> Remove(string slug, Guid userId, CancellationToken ct)
    {
        await members.RemoveAsync(userId, ct);
        return NoContent();
    }
}
