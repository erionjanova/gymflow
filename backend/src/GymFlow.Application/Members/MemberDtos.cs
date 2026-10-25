using GymFlow.Domain.Enums;

namespace GymFlow.Application.Members;

public record MemberDto(
    Guid UserId,
    string FullName,
    string Email,
    string? PhoneNumber,
    TenantRole Role,
    DateTimeOffset JoinedAt);

public record ChangeRoleRequest(TenantRole Role);
