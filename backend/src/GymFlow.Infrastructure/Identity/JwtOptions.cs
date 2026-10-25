namespace GymFlow.Infrastructure.Identity;

public static class JwtClaims
{
    public const string Subject = "sub";
    public const string Name = "name";
    public const string Role = "role";
}

public static class PlatformRoles
{
    public const string Admin = "PlatformAdmin";
}

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "gymflow-api";
    public string Audience { get; set; } = "gymflow-web";

    public string Secret { get; set; } = string.Empty;

    public int ExpiryMinutes { get; set; } = 60 * 24 * 7;
}
