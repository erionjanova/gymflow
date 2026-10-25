using GymFlow.Application.Auth;
using GymFlow.Domain.Enums;

namespace GymFlow.Application.Invitations;

public record CreateInvitationRequest(string Email, TenantRole Role);

public record InvitationDto(
    Guid Id,
    string Email,
    TenantRole Role,
    InvitationStatus Status,
    string InvitedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt);

public record InvitationPreviewDto(
    string GymName,
    string GymSlug,
    string BrandColor,
    string Email,
    TenantRole Role,
    InvitationStatus Status,
    bool HasAccount);

public record AcceptInvitationRequest(string? FullName, string? Password, string? PreferredLanguage);

public record AcceptInvitationResponse(MyGymDto Gym, AuthResponse? Auth);
