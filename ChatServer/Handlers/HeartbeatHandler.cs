using Chat.Contracts;
using ChatServer.Infrastructure;
using ChatServer.Services;
using EasyNetQ;

namespace ChatServer.Handlers;

public class HeartbeatHandler
{
    private readonly IBus _bus;
    private readonly HeartbeatService _service;

    private const string SubscriptionId = TopicNames.HeartbeatSubscription;

    public HeartbeatHandler(IBus bus, HeartbeatService service)
    {
        _bus = bus;
        _service = service;
    }

    /// <summary>
    /// Registriert die Subscription für Heartbeat-Nachrichten.
    /// </summary>
    public Task StartAsync()
    {
        return _bus.PubSub.SubscribeAsync<Heartbeat>(
            SubscriptionId,
            HandleAsync
        );
    }

    private Task HandleAsync(Heartbeat heartbeat)
    {
        // Delegiert an HeartbeatService
        _service.Handle(heartbeat.Username);
        return Task.CompletedTask;
    }
}
