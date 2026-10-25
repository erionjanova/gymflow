using GymFlow.Application.Common.Interfaces;
using GymFlow.Infrastructure.Identity;

namespace GymFlow.Api.Auth;

public class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public Guid? UserId =>
        Guid.TryParse(httpContextAccessor.HttpContext?.User.FindFirst(JwtClaims.Subject)?.Value, out var id) ? id : null;

    public bool IsPlatformAdmin => httpContextAccessor.HttpContext?.User.IsInRole(PlatformRoles.Admin) ?? false;
}
