using Chat.Contracts;
using ChatServer.Infrastructure;
using ChatServer.Services;
using EasyNetQ;

namespace ChatServer.Handlers;

public class PrivateMessageHandler
{
    private readonly IBus _bus;
    private readonly PrivateMessageService _service;

    private const string SubscriptionId = TopicNames.PrivateMessageSubscription;

    public PrivateMessageHandler(IBus bus, PrivateMessageService service)
    {
        _bus = bus;
        _service = service;
    }

    public Task StartAsync()
    {
        return _bus.PubSub.SubscribeAsync<SendPrivateMessageCommand>(
            SubscriptionId,
            HandleAsync
        );
    }

    private Task HandleAsync(SendPrivateMessageCommand command)
    {
        return _service.HandleAsync(command);
    }
}
