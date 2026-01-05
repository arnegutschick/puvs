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
public class FileHandler
{
    private readonly IBus _bus;
    private readonly FileService _service;

    // Subscription ID for file transfer commands
    private const string SubscriptionId = TopicNames.FileSubscription;

    /// <summary>
    /// Initializes the FileHandler with a message bus and file service.
    /// </summary>
    /// <param name="bus">The EasyNetQ message bus for Pub/Sub communication.</param>
    /// <param name="service">The service responsible for handling file transfers.</param>
    public FileHandler(IBus bus, FileService service)
    {
        _bus = bus;
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
            await _bus.PubSub.SubscribeAsync<SendFileCommand>(
                SubscriptionId,
                HandleAsync
            );
        }
        catch (Exception)
        {
            Console.WriteLine($"[ERROR] Failed to subscribe to client filesend events. Maybe RabbitMQ is down?");
        }
    }


    /// <summary>
    /// Handles an incoming <see cref="SendFileCommand"/> by delegating it
    /// to <see cref="FileService.HandleAsync"/>.
    /// </summary>
    /// <param name="command">The file command containing sender, file name, content, and size.</param>
    private async Task HandleAsync(SendFileCommand command)
    {
        if (command == null)
        {
            Console.WriteLine("[WARNING] Received null SendFileCommand");
            return;
        }

        try
        {
            // Delegate file processing to the service
            await _service.HandleAsync(command);
        }
        catch (Exception)
        {
            // Log the error on the server
            Console.WriteLine(
                $"[ERROR] Failed to process file from '{command.SenderUsername}' " +
                $"('{command.FileName}', {command.FileSizeBytes} bytes)."
            );

            // Notify the sender client about the failure
            try
            {
                string senderTopic = TopicNames.CreatePrivateUserTopicName(command.SenderUsername);
                var errorEvent = new ErrorEvent($"Failed to send file '{command.FileName}'. Please try again.");
                await _bus.PubSub.PublishAsync(errorEvent, senderTopic);
            }
            catch (Exception innerEx)
            {
                Console.Error.WriteLine($"[ERROR] Failed to send ErrorEvent to '{command.SenderUsername}': {innerEx}");
            }
        }
    }
}
