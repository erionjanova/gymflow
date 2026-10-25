using FluentValidation;

namespace GymFlow.Application.Classes;

public static class ClassRules
{
    public const int MaxCapacity = 500;
}

public class UpsertClassRequestValidator : AbstractValidator<UpsertClassRequest>
{
    public UpsertClassRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.DurationMinutes).InclusiveBetween(10, 240);
        RuleFor(x => x.DefaultCapacity).InclusiveBetween(1, ClassRules.MaxCapacity);
    }
}
