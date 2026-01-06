using Chat.Contracts;
using System.Collections.Concurrent;

namespace ChatServer.Services;

/// <summary>
/// Handles private messages between users by creating message events 
/// for both the sender and the recipient.
/// Throws an exception if the recipient does not exist or is offline.
/// </summary>
public class PrivateMessageService
{
    private readonly ConcurrentDictionary<string, UserInfo> _users;

    /// <summary>
    /// Initializes the <see cref="PrivateMessageService"/> with a thread-safe collection
    /// of currently connected users.
    /// </summary>
    /// <param name="users">Thread-safe collection of currently connected users.</param>
    public PrivateMessageService(
        ConcurrentDictionary<string, UserInfo> users)
    {
        _users = users;
    }


    /// <summary>
    /// Processes an incoming private message command.
    /// - Creates a message event for the recipient
    /// - Creates a copy of the message event for the sender
    /// - Throws an exception if the recipient does not exist or is offline
    /// </summary>
    /// <param name="command">The <see cref="SendPrivateMessageCommand"/> containing sender, recipient, and message text.</param>
    /// <returns>
    /// A tuple containing:
    /// <list type="bullet">
    ///   <item><description><c>recipientEvent</c>: the <see cref="PrivateMessageEvent"/> for the recipient</description></item>
    ///   <item><description><c>senderEvent</c>: a copy of the <see cref="PrivateMessageEvent"/> for the sender</description></item>
    /// </list>
    /// </returns>
    public (PrivateMessageEvent recipientEvent, PrivateMessageEvent senderEvent) ProcessPrivateMessage(SendPrivateMessageCommand command)
    {
        if (command == null)
        {
            throw new ArgumentException("Received null SendPrivateMessageCommand");
        }

        Console.WriteLine(
            $"Private message from '{command.SenderUsername}' to '{command.RecipientUsername}': '{command.Text}'"
        );

        string recipientKey = command.RecipientUsername.Trim();

        // --- Check if recipient exists ---
        if (!_users.TryGetValue(recipientKey, out var recipientInfo))
        {
            throw new InvalidOperationException($"User '{command.RecipientUsername}' is not online or does not exist.");
        }

        // --- Create message for recipient ---
        var recipientEvent = new PrivateMessageEvent(
            command.SenderUsername,
            command.RecipientUsername,
            command.Text,
            recipientInfo.Color,
            IsOutgoing: false
        );
        // --- Create copy for sender ---
        var senderEvent = new PrivateMessageEvent(
            command.SenderUsername,
            command.RecipientUsername,
            command.Text,
            recipientInfo.Color,
            IsOutgoing: true
        );

        return (recipientEvent, senderEvent);
    }
}
