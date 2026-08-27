namespace Core.Notifications.Services;

public interface IFirebaseNotificationService
{
    Task<string> SendToTopicAsync(string topic, string title, string body, Dictionary<string, string>? data, CancellationToken cancellationToken);
    Task<string> SendToDevicesAsync(List<string> tokens, string title, string body, Dictionary<string, string>? data, CancellationToken cancellationToken);
    Task<string> SendToAllAsync(string title, string body, Dictionary<string, string>? data, CancellationToken cancellationToken);
    Task<string> MultilingualSendToTopicAsync(string topic, Dictionary<string, string> data, CancellationToken cancellationToken);
    Task<string> MultilingualSendToDevicesAsync(List<string> tokens, Dictionary<string, string> data, CancellationToken cancellationToken);
    Task<string> MultilingualSendToAllAsync(Dictionary<string, string> data, CancellationToken cancellationToken);

    Task SubscribeToTopicAsync(List<string> tokens, string topic);
    Task UnsubscribeFromTopicAsync(List<string> tokens, string topic);
}
