using Chat.Contracts;
using EasyNetQ;
using System.Collections.Concurrent;

namespace ChatServer.Services;

/// <summary>
/// Service responsible for handling broadcast messages from clients.
/// Updates statistics and publishes the messages to all connected clients via Pub/Sub.
/// </summary>
public class MessageService
{
    private readonly IBus _bus;
    private readonly ConcurrentDictionary<string, UserInfo> _users;
    private readonly StatisticsService _stats;

    /// <summary>
    /// Initializes the MessageService with a message bus, user dictionary, and statistics service.
    /// </summary>
    /// <param name="bus">The EasyNetQ message bus for publishing broadcast events.</param>
    /// <param name="users">Thread-safe dictionary of currently connected users.</param>
    /// <param name="stats">Service responsible for tracking message statistics.</param>
    public MessageService(
        IBus bus,
        ConcurrentDictionary<string, UserInfo> users,
        StatisticsService stats)
    {
        _bus = bus;
        _users = users;
        _stats = stats;
    }


    /// <summary>
    /// Handles an incoming message from a client.
    /// - Trims whitespace
    /// - Records the message in statistics if it is not a command (does not start with '/')
    /// - Determines the user's display color
    /// - Broadcasts the message to all clients
    /// </summary>
    /// <param name="command">The <see cref="SubmitMessageCommand"/> containing the username and text.</param>
    public async Task HandleAsync(SubmitMessageCommand command)
    {
        if (command == null)
        {
            Console.WriteLine("[WARNING] Received null SubmitMessageCommand");
            return;
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
        var evt = new BroadcastMessageEvent(
            command.SenderUsername,
            text,
            color
        );

        // --- Publish to all subscribers ---
        await _bus.PubSub.PublishAsync(evt);

        Console.WriteLine("Broadcasted message to all clients.");
    }
}
