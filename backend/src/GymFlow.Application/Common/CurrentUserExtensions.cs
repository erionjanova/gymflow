using GymFlow.Application.Common.Exceptions;
using GymFlow.Application.Common.Interfaces;

namespace GymFlow.Application.Common;

public static class CurrentUserExtensions
{
    public static Guid RequireUserId(this ICurrentUser currentUser) =>
        currentUser.UserId ?? throw new UnauthorizedException("not_authenticated", "You need to log in.");
}
