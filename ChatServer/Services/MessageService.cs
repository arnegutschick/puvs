using Chat.Contracts;
using System.Collections.Concurrent;

namespace ChatServer.Services;

/// <summary>
/// Handles broadcast messages from clients.
/// Trims, normalizes, and processes messages, updates statistics, 
/// determines user display color, and creates a broadcast message event.
/// </summary>
public class MessageService
{
    private readonly ConcurrentDictionary<string, UserInfo> _users;
    private readonly StatisticsService _stats;

    /// <summary>
    /// Initializes the <see cref="MessageService"/> with a dictionary of users and a statistics service.
    /// </summary>
    /// <param name="users">Thread-safe dictionary of currently connected users.</param>
    /// <param name="stats">Service responsible for tracking message statistics.</param>
    public MessageService(
        ConcurrentDictionary<string, UserInfo> users,
        StatisticsService stats)
    {
        _users = users;
        _stats = stats;
    }


    /// <summary>
    /// Processes an incoming client message.
    /// - Trims and normalizes the message text
    /// - Records the message in statistics if it is not a command (does not start with '/')
    /// - Determines the user's display color (defaults to Black if unknown)
    /// - Creates a <see cref="BroadcastMessageEvent"/> for further handling
    /// </summary>
    /// <param name="command">The <see cref="SubmitMessageCommand"/> containing the sender and message text.</param>
    /// <returns>
    /// A <see cref="BroadcastMessageEvent"/> containing the sender, the normalized message text, and the user's display color.
    /// </returns>
    public BroadcastMessageEvent ProcessMessage(SubmitMessageCommand command)
    {
        if (command == null)
        {
            throw new ArgumentException("Received null SubmitMessageCommand");
        }

        Console.WriteLine(
            $"Received message from '{command.SenderUsername}': '{command.Text}'"
        );

        // Normalize and trim the message text
        var text = command.Text?.Trim() ?? "";

        // --- Record statistics only for regular messages ---
        if (!text.StartsWith("/"))
            _stats.RecordMessage(command.SenderUsername);

        // --- Determine user's color, default to Black if unknown ---
        string color = _users.TryGetValue(command.SenderUsername, out var user)
            ? user.Color
            : "Black";

        // --- Create broadcast event ---
        var messageEvent = new BroadcastMessageEvent(
            command.SenderUsername,
            text,
            color
        );

        return messageEvent;
    }
}
