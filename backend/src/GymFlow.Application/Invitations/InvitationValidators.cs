using GymFlow.Application.Common;
using GymFlow.Domain.Enums;
using FluentValidation;

namespace GymFlow.Application.Invitations;

public class CreateInvitationRequestValidator : AbstractValidator<CreateInvitationRequest>
{
    public CreateInvitationRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(200);
        RuleFor(x => x.Role)
            .Must(r => r is TenantRole.Trainer or TenantRole.Owner)
            .WithMessage("You can invite trainers or co-owners.");
    }
}

public class NewAccountForInvitationValidator : AbstractValidator<AcceptInvitationRequest>
{
    public NewAccountForInvitationValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Password!).ValidPassword();
        RuleFor(x => x.PreferredLanguage!).ValidLanguage().When(x => x.PreferredLanguage is not null);
    }
}
