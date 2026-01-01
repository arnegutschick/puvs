using Chat.Contracts;
using Chat.Contracts.Infrastructure;
using ChatServer.Services;
using EasyNetQ;

namespace ChatServer.Handlers;

/// <summary>
/// Handler for public chat messages.
/// Subscribes to <see cref="SubmitMessageCommand"/> events and delegates
/// handling to <see cref="MessageService"/> for broadcasting to all clients.
/// </summary>
public class MessageHandler
{
    private readonly IBus _bus;
    private readonly MessageService _service;

    // Subscription ID for public messages
    private const string SubscriptionId = TopicNames.MessageSubscription;

    /// <summary>
    /// Initializes the MessageHandler with a message bus and message service.
    /// </summary>
    /// <param name="bus">The EasyNetQ message bus for Pub/Sub communication.</param>
    /// <param name="service">The service responsible for handling and broadcasting messages.</param>
    public MessageHandler(IBus bus, MessageService service)
    {
        _bus = bus;
        _service = service;
    }


    /// <summary>
    /// Starts the subscription to public message commands.
    /// Each received <see cref="SubmitMessageCommand"/> is handled asynchronously.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous subscription registration.</returns>
    public Task StartAsync()
    {
        // Subscribe to public message events
        return _bus.PubSub.SubscribeAsync<SubmitMessageCommand>(
            SubscriptionId,
            HandleAsync
        );
    }


    /// <summary>
    /// Handles an incoming <see cref="SubmitMessageCommand"/> by delegating it
    /// to <see cref="MessageService.HandleAsync"/>.
    /// </summary>
    /// <param name="command">The public message command containing sender and text.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous handling operation.</returns>
    private Task HandleAsync(SubmitMessageCommand command)
    {
        return _service.HandleAsync(command);
    }
}
