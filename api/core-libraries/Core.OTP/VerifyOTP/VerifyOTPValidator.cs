using Core.OTP.Exceptions;
using Core.Validation.Extensions;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace Core.OTP.VerifyOTP;

public sealed class VerifyOTPValidator : AbstractValidator<VerifyOTPCommand>
{
    public VerifyOTPValidator(IOptions<OtpOptions> options)
    {
        var otpOptions = options.Value;
        RuleFor(x => x.Code)
            .ValidateRequired(ErrorCodes.OtpInvalidFormat)
            .Length(otpOptions.OtpLength).WithErrorCode(ErrorCodes.OtpInvalidFormat);

        RuleFor(x => x.VerificationId)
            .ValidateRequired(ErrorCodes.OtpVerificationIdInvalidFormat);
    }
}
