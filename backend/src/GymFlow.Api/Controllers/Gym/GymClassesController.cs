using GymFlow.Api.Auth;
using GymFlow.Application.Classes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymFlow.Api.Controllers.Gym;

[ApiController]
[Authorize(Policy = Policies.GymOwner)]
[Route("api/g/{slug}/admin/classes")]
public class GymClassesController(ClassService classes) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<ClassDto>> GetAll(string slug, CancellationToken ct) =>
        classes.GetAsync(includeInactive: true, ct);

    [HttpPost]
    public async Task<ActionResult<ClassDto>> Create(string slug, UpsertClassRequest request, CancellationToken ct)
    {
        var gymClass = await classes.CreateAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created, gymClass);
    }

    [HttpPut("{id:guid}")]
    public Task<ClassDto> Update(string slug, Guid id, UpsertClassRequest request, CancellationToken ct) =>
        classes.UpdateAsync(id, request, ct);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(string slug, Guid id, CancellationToken ct)
    {
        await classes.DeleteAsync(id, ct);
        return NoContent();
    }
}
