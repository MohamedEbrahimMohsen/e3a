using Core.DDD.Entities;

namespace Core.Notifications.Entities;

public class UserDevice : Entity
{
    private UserDevice(Guid id) : base(id)
    {
    }

    public Guid? UserId { get; set; } = null;

    public string DeviceId { get; set; } = null!;
    public string PushToken { get; set; } = null!;
    public DevicePlatform Platform { get; set; }
    public string? DeviceName { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
    public bool IsActive { get; set; } = true;
    public string? AppVersion { get; set; }
    public string? OSVersion { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastNotificationSentAt { get; set; }

    public static UserDevice Create(Guid? userId, string deviceId, string pushToken, DevicePlatform platform, string? deviceName, string? appVersion, string? osVersion)
    {
        return new UserDevice(Guid.NewGuid())
        {
            UserId = userId,
            DeviceId = deviceId,
            PushToken = pushToken,
            Platform = platform,
            DeviceName = deviceName,
            AppVersion = appVersion,
            OSVersion = osVersion,
            LastSeenAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public void Update(Guid? userId, string pushToken, DevicePlatform platform, string? deviceName, string? appVersion, string? osVersion)
    {
        UserId = userId;
        PushToken = pushToken;
        Platform = platform;
        DeviceName = deviceName;
        AppVersion = appVersion;
        OSVersion = osVersion;
        LastSeenAt = DateTimeOffset.UtcNow;
    }
}

public enum DevicePlatform
{
    Android = 1,
    IOS = 2,
    Web = 3,
    Unknown = 100
}