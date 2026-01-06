using Chat.Contracts;

namespace ChatServer.Services;

/// <summary>
/// Service responsible for handling file transfers between clients.
/// Receives a file from a sender and broadcasts it to all connected clients.
/// </summary>
public class FileService
{
    /// <summary>
    /// Processes an incoming file sent by a client.
    /// - Validates the command and file size (throws if the file is too large)
    /// - Logs the received file
    /// - Creates a <see cref="BroadcastFileEvent"/> to be sent to all clients
    /// </summary>
    /// <param name="command">The <see cref="SendFileCommand"/> containing the sender, file name, file content, and file size.</param>
    /// <returns>
    /// A <see cref="BroadcastFileEvent"/> containing the sender, file name, base64-encoded content, and file size for broadcasting.
    /// </returns>
    public BroadcastFileEvent ProcessFile(SendFileCommand command)
    {
        if (command == null)
        {
            throw new ArgumentException("Received null SendFileCommand");
        }

        if (command.FileSizeBytes > 1_000_000)
            throw new InvalidOperationException("File too large");

        Console.WriteLine(
            $"File received from '{command.SenderUsername}': {command.FileName} ({command.FileSizeBytes} bytes)"
        );

        // Create the event to broadcast the file to all clients
        var fileEvent = new BroadcastFileEvent(
            command.SenderUsername,
            command.FileName,
            command.ContentBase64,
            command.FileSizeBytes
        );

        return fileEvent;
    }
}
