using FluentValidation;

namespace GymFlow.Application.Common;

public static class ValidationRules
{
    public static readonly string[] SupportedLanguages = ["sq", "en"];

    public static IRuleBuilderOptions<T, string?> ValidPhoneNumber<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Matches(@"^\+?[0-9\s\-()]{6,20}$")
            .WithMessage("Phone number is not valid.");

    public static IRuleBuilderOptions<T, string> ValidLanguage<T>(this IRuleBuilder<T, string> rule) =>
        rule.Must(lang => SupportedLanguages.Contains(lang))
            .WithMessage("Language must be 'sq' or 'en'.");

    public static IRuleBuilderOptions<T, string> ValidPassword<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty()
            .MinimumLength(8)
            .MaximumLength(100)
            .Matches("[A-Za-z]").WithMessage("Password must contain a letter.")
            .Matches("[0-9]").WithMessage("Password must contain a number.");

    public static IRuleBuilderOptions<T, string> ValidHexColor<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty()
            .Matches("^#[0-9a-fA-F]{6}$")
            .WithMessage("Color must be a hex value like #65a30d.");
}
