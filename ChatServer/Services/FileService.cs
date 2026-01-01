using EasyNetQ;

namespace ChatServer.Services;

public class FileService
{
    private readonly IBus _bus;

    public FileService(IBus bus)
    {
        _bus = bus;
    }

    public async Task HandleAsync(SendFileCommand command)
    {
        Console.WriteLine(
            $"File received from '{command.Sender}': {command.FileName} ({command.FileSizeBytes} bytes)"
        );

        var fileEvent = new BroadcastFileEvent(
            command.Sender,
            command.FileName,
            command.ContentBase64,
            command.FileSizeBytes
        );

        await _bus.PubSub.PublishAsync(fileEvent);
    }
}
