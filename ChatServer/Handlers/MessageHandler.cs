using Chat.Contracts;
using Chat.Contracts.Infrastructure;
using ChatServer.Services;
using EasyNetQ;

namespace ChatServer.Handlers;

public class MessageHandler
{
    private readonly IBus _bus;
    private readonly MessageService _service;

    private const string SubscriptionId = TopicNames.MessageSubscription;

    public MessageHandler(IBus bus, MessageService service)
    {
        _bus = bus;
        _service = service;
    }

    public Task StartAsync()
    {
        return _bus.PubSub.SubscribeAsync<SubmitMessageCommand>(
            SubscriptionId,
            HandleAsync
        );
    }

    private Task HandleAsync(SubmitMessageCommand command)
    {
        return _service.HandleAsync(command);
    }
}
