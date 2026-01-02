using Chat.Contracts;
using EasyNetQ;
using System.Collections.Concurrent;
using Chat.Contracts.Infrastructure;

namespace ChatServer.Services;

/// <summary>
/// Service responsible for handling private messages between users.
/// Publishes messages to both sender and recipient via topic-based Pub/Sub.
/// Sends error events if the recipient is not online.
/// </summary>
public class PrivateMessageService
{
    private readonly IBus _bus;
    private readonly ConcurrentDictionary<string, UserInfo> _users;

    /// <summary>
    /// Initializes the PrivateMessageService with a message bus and user dictionary.
    /// </summary>
    /// <param name="bus">The EasyNetQ message bus for publishing events.</param>
    /// <param name="users">Thread-safe collection of currently connected users.</param>
    public PrivateMessageService(
        IBus bus,
        ConcurrentDictionary<string, UserInfo> users)
    {
        _bus = bus;
        _users = users;
    }


    /// <summary>
    /// Handles an incoming private message command.
    /// Publishes the message to the recipient's topic and a copy to the sender's topic.
    /// If the recipient does not exist or is offline, publishes an <see cref="ErrorEvent"/> to the sender.
    /// </summary>
    /// <param name="command">The <see cref="SendPrivateMessageCommand"/> containing sender, recipient, and message text.</param>
    public async Task HandleAsync(SendPrivateMessageCommand command)
    {
        if (command == null)
        {
            Console.WriteLine("[WARNING] Received null SendPrivateMessageCommand");
            return;
        }

        Console.WriteLine(
            $"Private message from '{command.SenderUsername}' to '{command.RecipientUsername}': '{command.Text}'"
        );

        // Define sender topic
        string senderTopic = TopicNames.CreatePrivateUserTopicName(command.SenderUsername);
        string recipientKey = command.RecipientUsername.Trim();

        // --- Check if recipient exists ---
        if (!_users.TryGetValue(recipientKey, out var recipientInfo))
        {
            Console.WriteLine($"Aborted sending private message to non-existent user '{command.RecipientUsername}'.");

            // Publish error event to sender if recipient is offline or unknown
            var errorEvent = new ErrorEvent(
                $"User '{command.RecipientUsername}' is not online or does not exist."
            );

            await _bus.PubSub.PublishAsync(errorEvent, senderTopic);
            return;
        }

        // --- Send message to recipient ---
        var recipientEvent = new PrivateMessageEvent(
            command.SenderUsername,
            command.RecipientUsername,
            command.Text,
            recipientInfo.Color,
            IsOutgoing: false
        );

        string recipientTopic = TopicNames.CreatePrivateUserTopicName(command.RecipientUsername);
        await _bus.PubSub.PublishAsync(recipientEvent, recipientTopic);

        Console.WriteLine($"Sent private message to '{command.RecipientUsername}'.");

        // --- Send copy to sender ---
        var senderEvent = new PrivateMessageEvent(
            command.SenderUsername,
            command.RecipientUsername,
            command.Text,
            recipientInfo.Color,
            IsOutgoing: true
        );

        await _bus.PubSub.PublishAsync(senderEvent, senderTopic);
    }
}
