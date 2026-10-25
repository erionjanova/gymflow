using GymFlow.Api.Auth;
using GymFlow.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GymFlow.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(AuthService auth) : ControllerBase
{
    [HttpPost("register")]
    [EnableRateLimiting(RateLimits.Auth)]
    public Task<AuthResponse> Register(RegisterRequest request, CancellationToken ct) =>
        auth.RegisterAsync(request, ct);

    [HttpPost("login")]
    [EnableRateLimiting(RateLimits.Auth)]
    public Task<AuthResponse> Login(LoginRequest request, CancellationToken ct) =>
        auth.LoginAsync(request, ct);

    [Authorize]
    [HttpGet("me")]
    public Task<UserDto> Me(CancellationToken ct) => auth.GetCurrentAsync(ct);

    [Authorize]
    [HttpPut("me")]
    public Task<UserDto> UpdateMe(UpdateProfileRequest request, CancellationToken ct) =>
        auth.UpdateProfileAsync(request, ct);

    [Authorize]
    [HttpPut("me/password")]
    [EnableRateLimiting(RateLimits.Auth)]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken ct)
    {
        await auth.ChangePasswordAsync(request, ct);
        return NoContent();
    }

    [Authorize]
    [HttpGet("me/gyms")]
    public Task<IReadOnlyList<MyGymDto>> MyGyms(CancellationToken ct) => auth.GetMyGymsAsync(ct);
}
