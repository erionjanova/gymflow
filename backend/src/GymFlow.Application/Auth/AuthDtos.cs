using GymFlow.Domain.Entities;
using GymFlow.Domain.Enums;

namespace GymFlow.Application.Auth;

public record RegisterRequest(
    string FullName,
    string Email,
    string Password,
    string? PhoneNumber,
    string PreferredLanguage);

public record LoginRequest(string Email, string Password);

public record UpdateProfileRequest(string FullName, string? PhoneNumber, string PreferredLanguage);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public record UserDto(
    Guid Id,
    string FullName,
    string Email,
    string? PhoneNumber,
    string PreferredLanguage,
    bool IsPlatformAdmin)
{
    public static UserDto From(User user) =>
        new(user.Id, user.FullName, user.Email, user.PhoneNumber, user.PreferredLanguage, user.IsPlatformAdmin);
}

public record AuthResponse(string Token, DateTimeOffset ExpiresAt, UserDto User);

public record MyGymDto(Guid Id, string Name, string Slug, string BrandColor, TenantRole Role);

public record ForgotPasswordRequest(string Email);

public record ResetPasswordRequest(string Token, string NewPassword);
