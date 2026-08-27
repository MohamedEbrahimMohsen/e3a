namespace Core.Notifications.Services;

using FirebaseAdmin.Messaging;
using Microsoft.Extensions.Logging;

public class FirebaseNotificationService(FirebaseMessaging firebaseMessaging, ILogger<FirebaseNotificationService> logger) : IFirebaseNotificationService
{
    public async Task<string> SendToTopicAsync(string topic, string title, string body, Dictionary<string, string>? data, CancellationToken cancellationToken)
    {
        var message = new Message
        {
            Topic = topic,
            Notification = new Notification
            {
                Title = title,
                Body = body
            },
            Data = data
        };

        return await firebaseMessaging.SendAsync(message, cancellationToken).ConfigureAwait(false);
    }
    public async Task<string> SendToDevicesAsync(List<string> tokens, string title, string body, Dictionary<string, string>? data, CancellationToken cancellationToken)
    {
        var message = new MulticastMessage
        {
            Tokens = tokens,
            Notification = new Notification
            {
                Title = title,
                Body = body
            },
            Data = data
        };

        var results = await firebaseMessaging.SendEachForMulticastAsync(message, cancellationToken).ConfigureAwait(false);
        LogPartialFailures(results, tokens.Count);
        return results.Responses.Count(r => !r.IsSuccess).ToString();
    }
    public async Task<string> SendToAllAsync(string title, string body, Dictionary<string, string>? data, CancellationToken cancellationToken)
    {
        var message = new Message
        {
            Topic = "all_users",
            Notification = new Notification
            {
                Title = title,
                Body = body,
            },
            Data = data
        };
        return await firebaseMessaging.SendAsync(message, cancellationToken).ConfigureAwait(false);
    }

    public async Task<string> MultilingualSendToTopicAsync(string topic, Dictionary<string, string> data, CancellationToken cancellationToken)
    {
        var message = new Message
        {
            Topic = topic,
            Data = data,
        };

        return await firebaseMessaging.SendAsync(message, cancellationToken).ConfigureAwait(false);
    }
    public async Task<string> MultilingualSendToDevicesAsync(List<string> tokens, Dictionary<string, string> data, CancellationToken cancellationToken)
    {
        var message = new MulticastMessage
        {
            Tokens = tokens,
            Data = data
        };

        var results = await firebaseMessaging.SendEachForMulticastAsync(message, cancellationToken).ConfigureAwait(false);
        LogPartialFailures(results, tokens.Count);
        return results.Responses.Count(r => !r.IsSuccess).ToString();
    }
    public async Task<string> MultilingualSendToAllAsync(Dictionary<string, string> data, CancellationToken cancellationToken)
    {
        var message = new Message
        {
            Topic = "all_users",
            Data = data
        };

        return await firebaseMessaging.SendAsync(message, cancellationToken).ConfigureAwait(false);
    }

    public async Task SubscribeToTopicAsync(List<string> tokens, string topic)
    {
        await firebaseMessaging.SubscribeToTopicAsync(tokens, topic);
    }
    public async Task UnsubscribeFromTopicAsync(List<string> tokens, string topic)
    {
        await firebaseMessaging.UnsubscribeFromTopicAsync(tokens, topic);
    }

    // Multicast sends never throw on per-token failures; the failure count alone hides which tokens
    // died and why (dead token vs. quota vs. bad payload). Surface a per-reason breakdown when any fail.
    private void LogPartialFailures(BatchResponse results, int total)
    {
        if (results.FailureCount == 0)
        {
            return;
        }

        var reasons = results.Responses
            .Where(r => !r.IsSuccess)
            .GroupBy(r => r.Exception?.MessagingErrorCode?.ToString() ?? "Unknown")
            .ToDictionary(g => g.Key, g => g.Count());

        logger.LogWarning("PUSH_MULTICAST_PARTIAL_FAILURE {FailureCount}/{Total} {@Reasons}",
            results.FailureCount, total, reasons);
    }
}
