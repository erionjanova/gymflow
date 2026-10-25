using GymFlow.Api.Auth;
using GymFlow.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GymFlow.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(AuthService auth, PasswordResetService passwordReset) : ControllerBase
{
    [HttpPost("register")]
    [EnableRateLimiting(RateLimits.Auth)]
    public Task<AuthResponse> Register(RegisterRequest request, CancellationToken ct) =>
        auth.RegisterAsync(request, ct);

    [HttpPost("login")]
    [EnableRateLimiting(RateLimits.Auth)]
    public Task<AuthResponse> Login(LoginRequest request, CancellationToken ct) =>
        auth.LoginAsync(request, ct);

    [HttpPost("forgot-password")]
    [EnableRateLimiting(RateLimits.Auth)]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request, CancellationToken ct)
    {
        await passwordReset.RequestAsync(request, ct);
        return NoContent();
    }

    [HttpPost("reset-password")]
    [EnableRateLimiting(RateLimits.Auth)]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken ct)
    {
        await passwordReset.ResetAsync(request, ct);
        return NoContent();
    }

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
