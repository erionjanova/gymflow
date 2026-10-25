using GymFlow.Application.Tenancy;
using GymFlow.Domain.Enums;
using Microsoft.AspNetCore.Authorization;

namespace GymFlow.Api.Auth;

public class TenantRoleRequirement(params TenantRole[] roles) : IAuthorizationRequirement
{
    public IReadOnlyCollection<TenantRole> Roles { get; } = roles;
}

public class TenantRoleHandler(ITenantContext tenantContext) : AuthorizationHandler<TenantRoleRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, TenantRoleRequirement requirement)
    {
        if (tenantContext.TenantId is not null
            && tenantContext.Role is { } role
            && requirement.Roles.Contains(role))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
