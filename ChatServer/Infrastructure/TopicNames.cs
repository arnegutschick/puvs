namespace ChatServer.Infrastructure;

public static class TopicNames
{
    // -------------------------
    // Pub/Sub Subscriptions
    // -------------------------
    public const string MessageSubscription = "chat.sub.submit-message";
    public const string LogoutSubscription = "chat.sub.logout";
    public const string PrivateMessageSubscription = "chat.sub.private-message";
    public const string FileSubscription = "chat.sub.file";
    public const string HeartbeatSubscription = "chat.sub.heartbeat";

    // -------------------------
    // Dynamic Topics
    // -------------------------
    public static string CreatePrivateUserTopicName(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("Username must not be empty.", nameof(username));

        return $"chat.private.{username.ToLowerInvariant()}";
    }
}
