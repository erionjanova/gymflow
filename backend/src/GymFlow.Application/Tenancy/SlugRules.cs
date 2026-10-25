using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace GymFlow.Application.Tenancy;

public static partial class SlugRules
{
    public const int MinLength = 3;
    public const int MaxLength = 40;

    public static readonly HashSet<string> Reserved =
    [
        "admin", "api", "app", "auth", "billing", "dashboard", "g", "gym", "gyms",
        "help", "login", "logout", "platform", "register", "settings", "signup",
        "static", "support", "www"
    ];

    public static string Normalize(string value)
    {
        var decomposed = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                builder.Append(c);
        }

        var slug = NonSlugChars().Replace(builder.ToString(), "-");
        return RepeatedDashes().Replace(slug, "-").Trim('-');
    }

    public static bool IsValid(string slug) =>
        slug.Length is >= MinLength and <= MaxLength
        && ValidSlug().IsMatch(slug)
        && !Reserved.Contains(slug);

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonSlugChars();

    [GeneratedRegex("-{2,}")]
    private static partial Regex RepeatedDashes();

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex ValidSlug();
}
