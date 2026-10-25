using GymFlow.Application.Classes;
using FluentValidation;

namespace GymFlow.Application.Sessions;

public static class SessionRules
{
    public const int MaxScheduleDays = 92;
    public const int MaxSessionsPerSchedule = 200;
    public const int MaxRangeDays = 62;
    public static readonly TimeSpan AttendanceOpensBefore = TimeSpan.FromMinutes(30);
}

public class CreateSessionRequestValidator : AbstractValidator<CreateSessionRequest>
{
    public CreateSessionRequestValidator()
    {
        RuleFor(x => x.GymClassId).NotEmpty();
        RuleFor(x => x.Capacity).InclusiveBetween(1, ClassRules.MaxCapacity).When(x => x.Capacity is not null);
    }
}

public class ScheduleSessionsRequestValidator : AbstractValidator<ScheduleSessionsRequest>
{
    public ScheduleSessionsRequestValidator()
    {
        RuleFor(x => x.GymClassId).NotEmpty();
        RuleFor(x => x.DaysOfWeek).NotEmpty().WithMessage("Pick at least one day.");
        RuleForEach(x => x.DaysOfWeek).IsInEnum();
        RuleFor(x => x.ToDate).GreaterThanOrEqualTo(x => x.FromDate).WithMessage("End date must be after the start date.");
        RuleFor(x => x)
            .Must(x => x.ToDate.DayNumber - x.FromDate.DayNumber < SessionRules.MaxScheduleDays)
            .WithName("toDate")
            .WithMessage($"You can schedule at most {SessionRules.MaxScheduleDays} days at once.");
        RuleFor(x => x.Capacity).InclusiveBetween(1, ClassRules.MaxCapacity).When(x => x.Capacity is not null);
    }
}

public class UpdateSessionRequestValidator : AbstractValidator<UpdateSessionRequest>
{
    public UpdateSessionRequestValidator()
    {
        RuleFor(x => x.Capacity).InclusiveBetween(1, ClassRules.MaxCapacity);
    }
}
