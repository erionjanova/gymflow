using FluentValidation;

namespace GymFlow.Application.Plans;

public class UpsertPlanRequestValidator : AbstractValidator<UpsertPlanRequest>
{
    public UpsertPlanRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.Price).InclusiveBetween(0, 10_000).PrecisionScale(10, 2, ignoreTrailingZeros: true);
        RuleFor(x => x.Interval).IsInEnum();
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
    }
}
