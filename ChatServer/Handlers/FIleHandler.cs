using ChatServer.Infrastructure;
using ChatServer.Services;
using EasyNetQ;

namespace ChatServer.Handlers;

public class FileHandler
{
    private readonly IBus _bus;
    private readonly FileService _service;

    private const string SubscriptionId = TopicNames.FileSubscription;

    public FileHandler(IBus bus, FileService service)
    {
        _bus = bus;
        _service = service;
    }

    public Task StartAsync()
    {
        return _bus.PubSub.SubscribeAsync<SendFileCommand>(
            SubscriptionId,
            HandleAsync
        );
    }

    private Task HandleAsync(SendFileCommand command)
    {
        return _service.HandleAsync(command);
    }
}
