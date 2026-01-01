using Chat.Contracts;
using Chat.Contracts.Infrastructure;
using ChatServer.Services;
using EasyNetQ;

namespace ChatServer.Handlers;

/// <summary>
/// Handler for heartbeat messages from clients.
/// Subscribes to <see cref="Heartbeat"/> events and updates the user's last heartbeat timestamp
/// via <see cref="HeartbeatService"/>.
/// </summary>
public class HeartbeatHandler
{
    private readonly IBus _bus;
    private readonly HeartbeatService _service;

    // Subscription ID for heartbeat messages
    private const string SubscriptionId = TopicNames.HeartbeatSubscription;

    /// <summary>
    /// Initializes the HeartbeatHandler with a message bus and heartbeat service.
    /// </summary>
    /// <param name="bus">The EasyNetQ message bus for Pub/Sub communication.</param>
    /// <param name="service">The service responsible for handling heartbeat updates.</param>
    public HeartbeatHandler(IBus bus, HeartbeatService service)
    {
        _bus = bus;
        _service = service;
    }


    /// <summary>
    /// Starts the subscription to heartbeat messages.
    /// Each received <see cref="Heartbeat"/> event updates the user's heartbeat timestamp.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous subscription registration.</returns>
    public Task StartAsync()
    {
        // Subscribe to heartbeat events
        return _bus.PubSub.SubscribeAsync<Heartbeat>(
            SubscriptionId,
            HandleAsync
        );
    }
    

    /// <summary>
    /// Handles an incoming <see cref="Heartbeat"/> event by updating the user's heartbeat.
    /// </summary>
    /// <param name="heartbeat">The heartbeat event containing the username.</param>
    /// <returns>A completed <see cref="Task"/>.</returns>
    private Task HandleAsync(Heartbeat heartbeat)
    {
        // Delegate to HeartbeatService to update last heartbeat timestamp
        _service.Handle(heartbeat.Username);
        return Task.CompletedTask;
    }
}
