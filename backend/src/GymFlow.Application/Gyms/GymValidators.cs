using GymFlow.Application.Common;
using GymFlow.Application.Tenancy;
using FluentValidation;

namespace GymFlow.Application.Gyms;

public static class GymRules
{
    public static readonly string[] SupportedCurrencies = ["EUR", "USD", "GBP", "CHF"];

    public static IRuleBuilderOptions<T, string> ValidCurrency<T>(this IRuleBuilder<T, string> rule) =>
        rule.Must(c => SupportedCurrencies.Contains(c))
            .WithMessage($"Currency must be one of: {string.Join(", ", SupportedCurrencies)}.");
}

public class CreateGymRequestValidator : AbstractValidator<CreateGymRequest>
{
    public CreateGymRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Slug)
            .Must(s => SlugRules.IsValid(s!))
            .When(x => !string.IsNullOrWhiteSpace(x.Slug))
            .WithMessage($"Address must be {SlugRules.MinLength}-{SlugRules.MaxLength} lowercase letters, numbers or dashes, and not a reserved word.");
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.Address).MaximumLength(200);
        RuleFor(x => x.Phone).ValidPhoneNumber().When(x => !string.IsNullOrWhiteSpace(x.Phone));
        RuleFor(x => x.Currency).ValidCurrency();
    }
}

public class UpdateGymRequestValidator : AbstractValidator<UpdateGymRequest>
{
    public UpdateGymRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.Address).MaximumLength(200);
        RuleFor(x => x.Phone).ValidPhoneNumber().When(x => !string.IsNullOrWhiteSpace(x.Phone));
        RuleFor(x => x.BrandColor).ValidHexColor();
        RuleFor(x => x.Currency).ValidCurrency();
        RuleFor(x => x.TimeZone).NotEmpty().Must(GymTime.IsValidTimeZone).WithMessage("Time zone is not valid.");
    }
}
