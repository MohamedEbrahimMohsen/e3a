using Core.Notifications.Exceptions;
using Core.Validation.Extensions;
using FluentValidation;

namespace Core.Notifications.RegisterUserDevice;

public sealed class RegisterUserDeviceValidator : AbstractValidator<RegisterUserDeviceCommand>
{
    public RegisterUserDeviceValidator()
    {
        RuleFor(x => x.DeviceId)
            .ValidateRequired(ErrorCodes.UserDeviceIdRequired);

        RuleFor(x => x.PushToken)
            .ValidateRequired(ErrorCodes.PushTokenRequired);

        //RuleFor(x => x.Platform)
        //    .Must(x => !string.IsNullOrEmpty(x) &&
        //               (x.Equals("Android", StringComparison.OrdinalIgnoreCase) ||
        //               x.Equals("IOS", StringComparison.OrdinalIgnoreCase) ||
        //               x.Equals("Web", StringComparison.OrdinalIgnoreCase)))
        //    .WithErrorCode(ErrorCodes.DevicePlatformRequired);
    }
}
