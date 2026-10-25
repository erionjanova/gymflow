namespace GymFlow.Api.Auth;

public static class Policies
{
    public const string PlatformAdmin = "PlatformAdmin";

    public const string GymOwner = "GymOwner";

    public const string GymStaff = "GymStaff";

    public const string GymMember = "GymMember";
}

public static class RateLimits
{
    public const string Auth = "auth";
}
