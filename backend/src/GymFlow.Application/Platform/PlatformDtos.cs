namespace GymFlow.Application.Platform;

public record PlatformGymDto(
    Guid Id,
    string Name,
    string Slug,
    bool IsSuspended,
    DateTimeOffset CreatedAt,
    int MemberCount,
    string? OwnerEmail);

public record PlatformStatsDto(int Gyms, int ActiveGyms, int Users, int Memberships);

public record SetSuspendedRequest(bool IsSuspended);
