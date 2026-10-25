using GymFlow.Api.Auth;
using GymFlow.Application.Bookings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymFlow.Api.Controllers.Gym;

[ApiController]
[Authorize(Policy = Policies.GymMember)]
[Route("api/g/{slug}")]
public class GymBookingsController(BookingService bookings) : ControllerBase
{
    [HttpPost("sessions/{sessionId:guid}/booking")]
    public async Task<ActionResult<BookingDto>> Book(string slug, Guid sessionId, CancellationToken ct)
    {
        var booking = await bookings.BookAsync(sessionId, ct);
        return StatusCode(StatusCodes.Status201Created, booking);
    }

    [HttpDelete("sessions/{sessionId:guid}/booking")]
    public Task<BookingDto> Cancel(string slug, Guid sessionId, CancellationToken ct) =>
        bookings.CancelAsync(sessionId, ct);

    [HttpGet("me/bookings")]
    public Task<IReadOnlyList<BookingDto>> Mine(
        string slug, [FromQuery] BookingScope scope = BookingScope.Upcoming, CancellationToken ct = default) =>
        bookings.GetMineAsync(scope, ct);
}
