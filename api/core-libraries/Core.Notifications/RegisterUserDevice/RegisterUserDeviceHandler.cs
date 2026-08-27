using Core.Identity.Tokens.CurrentUser;
using Core.Notifications.Entities;
using Core.Notifications.Repositories;
using Core.Notifications.Services;
using MediatR;

namespace Core.Notifications.RegisterUserDevice;

public sealed class RegisterUserDeviceHandler(IUserDeviceRepository userDevicesRepository, ICurrentUserService currentUserService, IFirebaseNotificationService firebaseNotificationService) : IRequestHandler<RegisterUserDeviceCommand>
{
    public async Task Handle(RegisterUserDeviceCommand request, CancellationToken cancellationToken)
    {
        var userDevice = (await userDevicesRepository.FindAsync(device => device.DeviceId == request.DeviceId, cancellationToken)).FirstOrDefault();
        var platform = Enum.TryParse<DevicePlatform>(request.Platform, ignoreCase: true, out var result) ? result : DevicePlatform.Unknown;

        if (userDevice == null)
        {
            var newDevice = UserDevice.Create(currentUserService.UserId, request.DeviceId, request.PushToken, platform, request.DeviceName, request.AppVersion, request.OSVersion);
            await userDevicesRepository.AddAsync(newDevice, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            userDevice.Update(currentUserService.UserId, request.PushToken, platform, request.DeviceName, request.AppVersion, request.OSVersion);
        }

        await firebaseNotificationService.SubscribeToTopicAsync([request.PushToken], "all_users").ConfigureAwait(false);
        await userDevicesRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}