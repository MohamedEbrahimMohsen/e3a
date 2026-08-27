using Core.DDD.Entities;
using MediatR;

namespace Core.Notifications.RegisterUserDevice;

public sealed record RegisterUserDeviceCommand(
    string DeviceId, 
    string PushToken, 
    string Platform, 
    string? DeviceName, 
    string? AppVersion, 
    string? OSVersion) : IRequest;
