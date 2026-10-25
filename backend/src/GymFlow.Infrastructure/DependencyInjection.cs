using GymFlow.Application.Common;
using GymFlow.Application.Common.Interfaces;
using GymFlow.Infrastructure.Email;
using GymFlow.Infrastructure.Identity;
using GymFlow.Infrastructure.Payments;
using GymFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GymFlow.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Database")
            ?? throw new InvalidOperationException("Connection string 'Database' is missing.");

        services.AddDbContext<ApplicationDbContext>(options => options
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention());
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<DatabaseInitializer>();

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(o => o.Secret.Length >= 32, "Jwt:Secret must be at least 32 characters.")
            .ValidateOnStart();
        services.Configure<SeedOptions>(configuration.GetSection(SeedOptions.SectionName));
        services.Configure<AppOptions>(configuration.GetSection(AppOptions.SectionName));
        services.Configure<SmtpOptions>(configuration.GetSection(SmtpOptions.SectionName));
        services.Configure<StripeOptions>(configuration.GetSection(StripeOptions.SectionName));

        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();

        services.AddSingleton<EmailQueue>();
        services.AddSingleton<IEmailSender, QueuedEmailSender>();
        services.AddHostedService<EmailDispatcher>();

        services.AddSingleton<StripePaymentGateway>();
        services.AddSingleton<DemoPaymentGateway>();
        services.AddSingleton<IPaymentGateway>(sp =>
        {
            var stripe = sp.GetRequiredService<StripePaymentGateway>();
            return !stripe.IsConfigured && configuration.GetValue<bool>("Payments:DemoMode")
                ? sp.GetRequiredService<DemoPaymentGateway>()
                : stripe;
        });
        services.AddScoped<StripeWebhookProcessor>();

        return services;
    }
}
