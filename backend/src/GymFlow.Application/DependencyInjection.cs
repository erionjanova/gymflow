using GymFlow.Application.Auth;
using GymFlow.Application.Gyms;
using GymFlow.Application.Members;
using GymFlow.Application.Plans;
using GymFlow.Application.Platform;
using GymFlow.Application.Tenancy;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace GymFlow.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        services.AddSingleton(TimeProvider.System);

        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
        services.AddScoped<TenantResolver>();

        services.AddScoped<AuthService>();
        services.AddScoped<GymService>();
        services.AddScoped<PlanService>();
        services.AddScoped<MemberService>();
        services.AddScoped<PlatformService>();

        return services;
    }
}
