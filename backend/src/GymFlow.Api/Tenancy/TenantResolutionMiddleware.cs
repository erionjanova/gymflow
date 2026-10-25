using GymFlow.Application.Tenancy;

namespace GymFlow.Api.Tenancy;

public class TenantResolutionMiddleware(RequestDelegate next)
{
    public const string RoutePrefix = "/api/g";

    public async Task InvokeAsync(HttpContext context, TenantResolver resolver)
    {
        if (TryGetSlug(context.Request.Path, out var slug))
            await resolver.ResolveAsync(slug, context.RequestAborted);

        await next(context);
    }

    private static bool TryGetSlug(PathString path, out string slug)
    {
        slug = string.Empty;
        if (!path.StartsWithSegments(RoutePrefix, StringComparison.OrdinalIgnoreCase, out var remaining))
            return false;

        var value = remaining.Value?.TrimStart('/') ?? string.Empty;
        var end = value.IndexOf('/');
        slug = end < 0 ? value : value[..end];
        return slug.Length > 0;
    }
}
