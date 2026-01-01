namespace Chat.Contracts.Infrastructure;

/// <summary>
/// Defines topic names used for RabbitMQ Pub/Sub messaging in the chat application.
/// </summary>
public static class TopicNames
{
    // --- Pub/Sub Subscription Topics ---
    public const string MessageSubscription = "chat.sub.submit-message"; // Topic for submitting chat messages to all subscribers
    public const string LogoutSubscription = "chat.sub.logout"; // Topic for notifying when a user logs out
    public const string PrivateMessageSubscription = "chat.sub.private-message"; // Topic for delivering private messages between users
    public const string FileSubscription = "chat.sub.file"; // Topic for sending files to subscribers
    public const string HeartbeatSubscription = "chat.sub.heartbeat"; // Topic for heartbeat messages to monitor active connections

    
    /// <summary>
    /// Generates a private topic name for a specific user.
    /// </summary>
    /// <param name="username">The username of the target user. Cannot be null, empty, or whitespace.</param>
    /// <returns>A string representing the private topic name for the user.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="username"/> is null, empty, or whitespace.</exception>
    public static string CreatePrivateUserTopicName(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("Username must not be empty.", nameof(username));

        return $"chat.private.{username.ToLowerInvariant()}";
    }
}
