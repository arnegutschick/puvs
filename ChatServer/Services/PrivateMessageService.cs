using Chat.Contracts;
using EasyNetQ;
using System.Collections.Concurrent;
using Chat.Contracts.Infrastructure;

namespace ChatServer.Services;

public class PrivateMessageService
{
    private readonly IBus _bus;
    private readonly ConcurrentDictionary<string, UserInfo> _users;

    public PrivateMessageService(
        IBus bus,
        ConcurrentDictionary<string, UserInfo> users)
    {
        _bus = bus;
        _users = users;
    }

    public async Task HandleAsync(SendPrivateMessageCommand command)
    {
        Console.WriteLine(
            $"Private message from '{command.SenderUsername}' to '{command.RecipientUsername}': '{command.Text}'"
        );

        string senderTopic = TopicNames.CreatePrivateUserTopicName(command.SenderUsername);
        string recipientKey = command.RecipientUsername.Trim();

        if (!_users.TryGetValue(recipientKey, out var recipientInfo))
        {
            // Fehler-Event statt PrivateMessageEvent
            var errorEvent = new ErrorEvent(
                $"User '{command.RecipientUsername}' is not online or does not exist."
            );

            await _bus.PubSub.PublishAsync(errorEvent, senderTopic);
            return;
        }

        // Nachricht an Empfänger
        var recipientEvent = new PrivateMessageEvent(
            command.SenderUsername,
            command.RecipientUsername,
            command.Text,
            recipientInfo.Color,
            false
        );

        string recipientTopic = TopicNames.CreatePrivateUserTopicName(command.RecipientUsername);
        await _bus.PubSub.PublishAsync(recipientEvent, recipientTopic);

        Console.WriteLine($"Sent private message to '{command.RecipientUsername}'.");

        // Kopie an Sender
        var senderEvent = new PrivateMessageEvent(
            command.SenderUsername,
            command.RecipientUsername,
            command.Text,
            recipientInfo.Color,
            true
        );

        await _bus.PubSub.PublishAsync(senderEvent, senderTopic);
    }
}
