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
public class PrivateMessageHandler : CommandExecutionWrapper
{
    private readonly PrivateMessageService _service;

    // Subscription ID for private messages
    private const string SubscriptionId = TopicNames.PrivateMessageSubscription;

    /// <summary>
    /// Initializes the PrivateMessageHandler with a message bus and service.
    /// </summary>
    /// <param name="bus">The EasyNetQ message bus for Pub/Sub communication.</param>
    /// <param name="service">The service responsible for processing private messages.</param>
    public PrivateMessageHandler(IBus bus, PrivateMessageService service)
        : base(bus)
    {
        _service = service;
    }


    /// <summary>
    /// Starts the subscription to private message commands.
    /// Each received <see cref="SendPrivateMessageCommand"/> is handled asynchronously.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous subscription registration.</returns>
    public async Task StartAsync()
    {
        try
        {
            // Subscribe to private message events
            await Bus.PubSub.SubscribeAsync<SendPrivateMessageCommand>(
                SubscriptionId,
                HandleAsync
            );
        }
        catch (Exception)
        {
            Console.WriteLine($"[ERROR] Failed to start private message RPC. Maybe RabbitMQ is down?");
            throw;
        }
    }


    /// <summary>
    /// Handles an incoming <see cref="SendPrivateMessageCommand"/> asynchronously.
    /// - Processes the private message and generates events for both sender and recipient
    /// - Publishes the events to the corresponding private topics via the message bus
    /// - Uses <see cref="ExecuteCommandAsync"/> to handle execution context and errors
    /// </summary>
    /// <param name="command">The <see cref="SendPrivateMessageCommand"/> containing the sender, recipient, and message text.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    private Task HandleAsync(SendPrivateMessageCommand command)
    {
        if (command == null)
        {
            Console.WriteLine("[WARNING] Received null SendPrivateMessageCommand");
            return Task.CompletedTask;
        }

        return ExecuteCommandAsync(
            command.SenderUsername,
            async () =>
            {
                // Delegate command processing to service
                var events = _service.ProcessPrivateMessage(command);

                // Get topic names for routing
                string senderTopic = TopicNames.CreatePrivateUserTopicName(command.SenderUsername);
                string recipientTopic = TopicNames.CreatePrivateUserTopicName(command.RecipientUsername);

                await Bus.PubSub.PublishAsync(events.recipientEvent, recipientTopic);
                await Bus.PubSub.PublishAsync(events.senderEvent, senderTopic);
            },
            errorMessage: $"Failed to send private message to '{command.RecipientUsername}'.");
    }
}
