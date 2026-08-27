using Core.Validation.Extensions;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace Core.OTP.GenerateOTP;

public sealed class GenerateOTPValidator : AbstractValidator<GenerateOTPCommand>
{

    public GenerateOTPValidator(IOptions<OtpOptions> options)
    {
        var otpOptions = options.Value;
        RuleFor(x => x.PhoneNumber)
            .ValidatePhoneNumber(otpOptions.PhoneCodes, otpOptions.PhoneLength);
    }
}
