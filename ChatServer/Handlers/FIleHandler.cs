using Chat.Contracts;
using Chat.Contracts.Infrastructure;
using ChatServer.Services;
using EasyNetQ;

namespace ChatServer.Handlers;

/// <summary>
/// Handler for file transfer commands.
/// Subscribes to <see cref="SendFileCommand"/> events and delegates
/// handling to <see cref="FileService"/> to broadcast files to all clients.
/// </summary>
public class FileHandler : CommandExecutionWrapper
{
    private readonly FileService _service;

    // Subscription ID for file transfer commands
    private const string SubscriptionId = TopicNames.FileSubscription;

    /// <summary>
    /// Initializes the FileHandler with a message bus and file service.
    /// </summary>
    /// <param name="bus">The EasyNetQ message bus for Pub/Sub communication.</param>
    /// <param name="service">The service responsible for handling file transfers.</param>
    public FileHandler(IBus bus, FileService service)
        : base(bus)
    {
        _service = service;
    }
    

    /// <summary>
    /// Starts the subscription to file transfer commands.
    /// Each received <see cref="SendFileCommand"/> is handled asynchronously.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous subscription registration.</returns>
    public async Task StartAsync()
    {
        try
        {
            // Subscribe to file transfer events
            await Bus.PubSub.SubscribeAsync<SendFileCommand>(
                SubscriptionId,
                HandleAsync
            );
        }
        catch (Exception)
        {
            Console.WriteLine($"[ERROR] Failed to subscribe to client filesend events. Maybe RabbitMQ is down?");
            throw;
        }
    }


    /// <summary>
    /// Handles an incoming <see cref="SendFileCommand"/> asynchronously.
    /// - Processes the file and publishes a <see cref="BroadcastFileEvent"/> via the message bus
    /// - Uses <see cref="ExecuteCommandAsync"/> to handle errors and execution context
    /// </summary>
    /// <param name="command">The <see cref="SendFileCommand"/> containing the sender, file name, content, and file size.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    private Task HandleAsync(SendFileCommand command)
    {
        if (command == null)
        {
            Console.WriteLine("[WARNING] Received null SendFileCommand");
            return Task.CompletedTask;
        }

        return ExecuteCommandAsync(
            username: command.SenderUsername,
            handlerAction: async () =>
            {
                // Delegate command processing to service
                BroadcastFileEvent fileEvent = _service.ProcessFile(command);
                await Bus.PubSub.PublishAsync(fileEvent);
            },
            errorMessage: $"Failed to send file '{command.FileName}'. Please try again."
        );
    }
}
