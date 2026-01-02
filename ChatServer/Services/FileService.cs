using Chat.Contracts;
using EasyNetQ;

namespace ChatServer.Services;

/// <summary>
/// Service responsible for handling file transfers between clients.
/// Receives a file from a sender and broadcasts it to all connected clients.
/// </summary>
public class FileService
{
    private readonly IBus _bus;

    /// <summary>
    /// Initializes the FileService with the message bus.
    /// </summary>
    /// <param name="bus">The EasyNetQ message bus for publishing file events.</param>
    public FileService(IBus bus)
    {
        _bus = bus;
    }


    /// <summary>
    /// Handles an incoming file transfer command.
    /// - Logs the received file
    /// - Creates a <see cref="BroadcastFileEvent"/>
    /// - Publishes the file event to all subscribers
    /// </summary>
    /// <param name="command">The <see cref="SendFileCommand"/> containing sender, file name, content, and size.</param>
    public async Task HandleAsync(SendFileCommand command)
    {
        Console.WriteLine(
            $"File received from '{command.Sender}': {command.FileName} ({command.FileSizeBytes} bytes)"
        );

        // Create the event to broadcast the file to all clients
        var fileEvent = new BroadcastFileEvent(
            command.Sender,
            command.FileName,
            command.ContentBase64,
            command.FileSizeBytes
        );

        // Publish the file event to the message bus
        await _bus.PubSub.PublishAsync(fileEvent);
    }
}
