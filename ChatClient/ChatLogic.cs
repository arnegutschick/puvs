using EasyNetQ;
using Chat.Contracts;
using Terminal.Gui;

namespace ChatClient;

public class ChatLogic
{
    private readonly IBus _bus;
    private readonly Action<string> _appendMessageCallback;
    private readonly Func<FileReceivedEvent, Dialog> _showFileDialogCallback;
    public string Username { get; }

    public ChatLogic(IBus bus, string username, Action<string> appendMessageCallback, Func<FileReceivedEvent, Dialog> showFileDialogCallback)
    {
        _bus = bus;
        Username = username;
        _appendMessageCallback = appendMessageCallback;
        _showFileDialogCallback = showFileDialogCallback;
    }

    /// <summary>
    /// Handles all possible subscriptions of the chat client to receive one of many possible events.
    /// </summary>
    public void SubscribeEvents()
    {
        string subscriptionId = $"chat_client_{Guid.NewGuid()}";

        _bus.PubSub.Subscribe<BroadcastMessageEvent>(subscriptionId, msg =>
        {
            Application.MainLoop.Invoke(() =>
            {
                _appendMessageCallback($"{msg.Username}: {msg.Text}");
            });
        });

        _bus.PubSub.Subscribe<UserNotification>(subscriptionId, note =>
        {
            Application.MainLoop.Invoke(() =>
            {
                _appendMessageCallback($"[INFO] {note.Text}");
            });
        });

        _bus.PubSub.Subscribe<FileReceivedEvent>(subscriptionId, file =>
        {
            if (file.Sender == Username) return;

            Application.MainLoop.Invoke(() =>
            {
                _appendMessageCallback($"[FILE] {file.Sender}: {file.FileName} ({file.FileSizeBytes} bytes)");
                var dialog = _showFileDialogCallback(file);
                Application.Run(dialog);
            });
        });
    }

    /// <summary>
    /// Sends a chat message to the server.
    /// </summary>
    public void SendMessage(string text)
    {
        _bus.PubSub.Publish(new SubmitMessageCommand(Username, text));
    }

    /// <summary>
    /// Ensures the sent file exists and is smaller than 1 MB in size. If so, sends the file to the server.
    /// </summary>
    public async void HandleSendFile(string path)
    {
        if (!File.Exists(path))
        {
            _appendMessageCallback("[ERROR] File does not exist.");
            return;
        }

        var info = new FileInfo(path);
        if (info.Length > 1_000_000)
        {
            _appendMessageCallback("[ERROR] File too large (max 1 MB).");
            return;
        }

        byte[] bytes = await File.ReadAllBytesAsync(path);
        string base64 = Convert.ToBase64String(bytes);

        _bus.PubSub.Publish(new SendFileCommand(Username, info.Name, base64, info.Length));
        _appendMessageCallback($"[YOU] Sent file {info.Name}");
    }

    /// <summary>
    /// Saves a given file in the Downloads/Chat folder.
    /// </summary>
    public async void SaveFile(FileReceivedEvent file)
    {
        byte[] data = Convert.FromBase64String(file.ContentBase64);

        string dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads",
            "Chat"
        );

        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, file.FileName);
        await File.WriteAllBytesAsync(path, data);

        _appendMessageCallback($"[SAVED] {path}");
    }
}
