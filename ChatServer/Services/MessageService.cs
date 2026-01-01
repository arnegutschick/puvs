using Chat.Contracts;
using EasyNetQ;
using System.Collections.Concurrent;

namespace ChatServer.Services;

public class MessageService
{
    private readonly IBus _bus;
    private readonly ConcurrentDictionary<string, UserInfo> _users;
    private readonly StatisticsService _stats;

    public MessageService(
        IBus bus,
        ConcurrentDictionary<string, UserInfo> users,
        StatisticsService stats)
    {
        _bus = bus;
        _users = users;
        _stats = stats;
    }

    public async Task HandleAsync(SubmitMessageCommand command)
    {
        Console.WriteLine(
            $"Received message from '{command.Username}': '{command.Text}'"
        );

        var text = command.Text?.Trim() ?? "";

        if (!text.StartsWith("/"))
            _stats.RecordMessage(command.Username);

        string color = _users.TryGetValue(command.Username, out var user)
            ? user.Color
            : "Black";

        var evt = new BroadcastMessageEvent(
            command.Username,
            text,
            color
        );

        await _bus.PubSub.PublishAsync(evt);

        Console.WriteLine("Broadcasted message to all clients.");
    }
}
