using Chat.Contracts;
using Chat.Contracts.Infrastructure;
using ChatServer.Services;
using EasyNetQ;

namespace ChatServer.Handlers;

/// <summary>
/// Handler for private messages sent between clients.
/// Subscribes to <see cref="SendPrivateMessageCommand"/> events and delegates
/// handling to <see cref="PrivateMessageService"/>.
/// </summary>
public class PrivateMessageHandler
{
    private readonly IBus _bus;
    private readonly PrivateMessageService _service;

    // Subscription ID for private messages
    private const string SubscriptionId = TopicNames.PrivateMessageSubscription;

    /// <summary>
    /// Initializes the PrivateMessageHandler with a message bus and service.
    /// </summary>
    /// <param name="bus">The EasyNetQ message bus for Pub/Sub communication.</param>
    /// <param name="service">The service responsible for processing private messages.</param>
    public PrivateMessageHandler(IBus bus, PrivateMessageService service)
    {
        _bus = bus;
        _service = service;
    }


    /// <summary>
    /// Starts the subscription to private message commands.
    /// Each received <see cref="SendPrivateMessageCommand"/> is handled asynchronously.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous subscription registration.</returns>
    public Task StartAsync()
    {
        // Subscribe to private message events
        return _bus.PubSub.SubscribeAsync<SendPrivateMessageCommand>(
            SubscriptionId,
            HandleAsync
        );
    }


    /// <summary>
    /// Handles an incoming <see cref="SendPrivateMessageCommand"/> by delegating it
    /// to <see cref="PrivateMessageService.HandleAsync"/>.
    /// </summary>
    /// <param name="command">The private message command containing sender, recipient, and text.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous handling operation.</returns>
    private Task HandleAsync(SendPrivateMessageCommand command)
    {
        return _service.HandleAsync(command);
    }
}
