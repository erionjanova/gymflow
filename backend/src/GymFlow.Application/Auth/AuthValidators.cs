using GymFlow.Application.Common;
using FluentValidation;

namespace GymFlow.Application.Auth;

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(200);
        RuleFor(x => x.Password).ValidPassword();
        RuleFor(x => x.PhoneNumber).ValidPhoneNumber().When(x => !string.IsNullOrWhiteSpace(x.PhoneNumber));
        RuleFor(x => x.PreferredLanguage).ValidLanguage();
    }
}

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}

public class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.PhoneNumber).ValidPhoneNumber().When(x => !string.IsNullOrWhiteSpace(x.PhoneNumber));
        RuleFor(x => x.PreferredLanguage).ValidLanguage();
    }
}
