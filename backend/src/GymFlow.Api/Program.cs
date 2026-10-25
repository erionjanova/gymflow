using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using GymFlow.Api.Auth;
using GymFlow.Api.ErrorHandling;
using GymFlow.Api.Tenancy;
using GymFlow.Application;
using GymFlow.Application.Common.Interfaces;
using GymFlow.Domain.Enums;
using GymFlow.Infrastructure;
using GymFlow.Infrastructure.Identity;
using GymFlow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

if (builder.Configuration["RENDER_EXTERNAL_URL"] is { Length: > 0 } publicUrl)
    builder.Configuration["App:FrontendUrl"] = publicUrl;

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<IAuthorizationHandler, TenantRoleHandler>();

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
    .ConfigureApiBehaviorOptions(o =>
    {
        o.InvalidModelStateResponseFactory = context =>
        {
            var problem = new ValidationProblemDetails(context.ModelState) { Status = StatusCodes.Status400BadRequest };
            problem.Extensions["code"] = "validation_failed";
            return new BadRequestObjectResult(problem);
        };
    });

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.MapInboundClaims = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret)),
            NameClaimType = JwtClaims.Name,
            RoleClaimType = JwtClaims.Role,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
        o.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                context.Response.Headers[SessionHeaders.Expired] = "true";
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Policies.PlatformAdmin, p => p.RequireRole(PlatformRoles.Admin))
    .AddPolicy(Policies.GymOwner, p => p
        .RequireAuthenticatedUser()
        .AddRequirements(new TenantRoleRequirement(TenantRole.Owner)))
    .AddPolicy(Policies.GymStaff, p => p
        .RequireAuthenticatedUser()
        .AddRequirements(new TenantRoleRequirement(TenantRole.Owner, TenantRole.Trainer)))
    .AddPolicy(Policies.GymMember, p => p
        .RequireAuthenticatedUser()
        .AddRequirements(new TenantRoleRequirement(TenantRole.Owner, TenantRole.Trainer, TenantRole.Member)));

builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy(RateLimits.Auth, context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
});

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()
    .WithExposedHeaders(SessionHeaders.Expired)));

var app = builder.Build();

if (app.Configuration.GetValue("Database:MigrateOnStartup", true))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().InitializeAsync();
}

app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        if (ctx.Context.Request.Path.StartsWithSegments("/assets"))
            ctx.Context.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
    }
});
app.UseRouting();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(o => o.WithTitle("GymFlow API"));
}

app.UseCors();
app.UseAuthentication();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();
app.MapHealthChecks("/health");
app.MapFallbackToFile("{*path:regex(^(?!api/|assets/).*$)}", "index.html");

app.Run();

public partial class Program;
