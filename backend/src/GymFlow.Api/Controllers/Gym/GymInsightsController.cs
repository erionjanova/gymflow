using GymFlow.Api.Auth;
using GymFlow.Application.Insights;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymFlow.Api.Controllers.Gym;

[ApiController]
[Route("api/g/{slug}/admin/insights")]
[Authorize(Policy = Policies.GymOwner)]
public class GymInsightsController(InsightsService insights) : ControllerBase
{
    [HttpGet]
    public Task<InsightsDto> Get(string slug, CancellationToken ct) => insights.GetAsync(ct);
}
