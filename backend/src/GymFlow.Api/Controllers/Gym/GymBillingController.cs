using GymFlow.Api.Auth;
using GymFlow.Application.Billing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymFlow.Api.Controllers.Gym;

[ApiController]
[Route("api/g/{slug}")]
public class GymBillingController(BillingService billing) : ControllerBase
{
    [Authorize(Policy = Policies.GymMember)]
    [HttpPost("plans/{planId:guid}/checkout")]
    public Task<CheckoutDto> Checkout(string slug, Guid planId, CancellationToken ct) =>
        billing.StartCheckoutAsync(planId, ct);

    [Authorize(Policy = Policies.GymMember)]
    [HttpGet("me/subscription")]
    public Task<MySubscriptionDto?> MySubscription(string slug, CancellationToken ct) => billing.GetMineAsync(ct);

    [Authorize(Policy = Policies.GymMember)]
    [HttpPut("me/subscription/cancellation")]
    public Task<MySubscriptionDto> SetCancellation(string slug, SetCancelRequest request, CancellationToken ct) =>
        billing.SetCancelAtPeriodEndAsync(request.CancelAtPeriodEnd, ct);

    [Authorize(Policy = Policies.GymOwner)]
    [HttpGet("admin/billing")]
    public Task<BillingOverviewDto> Overview(string slug, CancellationToken ct) => billing.GetOverviewAsync(ct);
}
