using GymFlow.Infrastructure.Payments;
using Microsoft.AspNetCore.Mvc;
using Stripe;

namespace GymFlow.Api.Controllers;

[ApiController]
[Route("api/payments")]
public class PaymentsController(StripeWebhookProcessor processor, ILogger<PaymentsController> logger) : ControllerBase
{
    [HttpPost("stripe/webhook")]
    public async Task<IActionResult> StripeWebhook(CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body);
        var json = await reader.ReadToEndAsync(ct);

        try
        {
            await processor.ProcessAsync(json, Request.Headers["Stripe-Signature"].ToString(), ct);
            return Ok();
        }
        catch (StripeException ex)
        {
            logger.LogWarning(ex, "Rejected Stripe webhook");
            return BadRequest();
        }
    }
}
