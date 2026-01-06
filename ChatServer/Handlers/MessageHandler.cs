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
public class MessageHandler : CommandExecutionWrapper
{
    private readonly MessageService _service;

    // Subscription ID for public messages
    private const string SubscriptionId = TopicNames.MessageSubscription;

    /// <summary>
    /// Initializes the MessageHandler with a message bus and message service.
    /// </summary>
    /// <param name="bus">The EasyNetQ message bus for Pub/Sub communication.</param>
    /// <param name="service">The service responsible for handling and broadcasting messages.</param>
    public MessageHandler(IBus bus, MessageService service)
        : base(bus)
    {
        _service = service;
    }


    /// <summary>
    /// Starts the subscription to public message commands.
    /// Each received <see cref="SubmitMessageCommand"/> is handled asynchronously.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous subscription registration.</returns>
    public async Task StartAsync()
    {
        try
        {
            // Subscribe to public message events
            await Bus.PubSub.SubscribeAsync<SubmitMessageCommand>(
                SubscriptionId,
                HandleAsync
            );
        }
        catch (Exception)
        {
            Console.WriteLine($"[ERROR] Failed to subscribe to client messages. Maybe RabbitMQ is down?");
            throw;
        }
    }


    /// <summary>
    /// Handles an incoming <see cref="SubmitMessageCommand"/> asynchronously.
    /// - Processes the message and publishes a <see cref="BroadcastMessageEvent"/> via the message bus
    /// - Uses <see cref="ExecuteCommandAsync"/> to handle execution and errors
    /// </summary>
    /// <param name="command">The <see cref="SubmitMessageCommand"/> containing the sender and message text.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    private Task HandleAsync(SubmitMessageCommand command)
    {
        if (command == null)
        {
            Console.WriteLine("[WARNING] Received null SubmitMessageCommand");
            return Task.CompletedTask;
        }

        return ExecuteCommandAsync(
            command.SenderUsername,
            handlerAction: async () =>
            {
                // Delegate command processing to service
                BroadcastMessageEvent messageEvent = _service.ProcessMessage(command);
                await Bus.PubSub.PublishAsync(messageEvent);
            },
            "Failed to send your message. Please try again."
        );
    }
}
