using EasyNetQ;
using Chat.Contracts;
using Terminal.Gui;

namespace ChatClient;

public class ChatLogic
{
    private readonly IBus _bus;
    private readonly Action<string, string> _appendMessageCallback;
    private readonly Func<FileReceivedEvent, Dialog> _showFileDialogCallback;
    private Timer? _heartbeatTimer;

    public string Username { get; }

    public ChatLogic(
        IBus bus,
        string username,
        Action<string, string> appendMessageCallback,
        Func<FileReceivedEvent, Dialog> showFileDialogCallback)
    {
        _bus = bus;
        Username = username;
        _appendMessageCallback = appendMessageCallback;
        _showFileDialogCallback = showFileDialogCallback;
    }

    /// <summary>
    /// Handles all subscriptions of the chat client (broadcast, notifications, files, private messages).
    /// </summary>
    public void SubscribeEvents()
    {
        string subscriptionId = $"chat_client_{Guid.NewGuid()}";
        string privateTopic = $"private_{Username.ToLowerInvariant()}";

        // --- Broadcast chat messages ---
        _bus.PubSub.Subscribe<BroadcastMessageEvent>(subscriptionId, msg =>
        {
            Application.MainLoop.Invoke(() =>
            {
                _appendMessageCallback($"{msg.Username}: {msg.Text}", msg.UserColor);
            });
        });

        // --- User notifications ---
        _bus.PubSub.Subscribe<UserNotification>(subscriptionId, note =>
        {
            Application.MainLoop.Invoke(() =>
            {
                _appendMessageCallback($"[INFO] {note.Text}", "Black");
            });
        });

        // --- File messages ---
        _bus.PubSub.Subscribe<FileReceivedEvent>(subscriptionId, file =>
        {
            if (file.Sender == Username) return;

            Application.MainLoop.Invoke(() =>
            {
                _appendMessageCallback(
                    $"[FILE] {file.Sender}: {file.FileName} ({file.FileSizeBytes} bytes)",
                    "BrightGreen"
                );

                var dialog = _showFileDialogCallback(file);
                Application.Run(dialog);
            });
        });

        // --- Private messages (topic-based) ---
        _bus.PubSub.Subscribe<PrivateMessageEvent>(
            privateTopic,
            privateMessage =>
            {
                Application.MainLoop.Invoke(() =>
                {
                    if (privateMessage.IsOutgoing)
                    {
                        _appendMessageCallback(
                            $"[PRIVATE → {privateMessage.RecipientUsername}] {privateMessage.Text}",
                            privateMessage.UserColor
                        );
                    }
                    else
                    {
                        _appendMessageCallback(
                            $"[PRIVATE ← {privateMessage.SenderUsername}] {privateMessage.Text}",
                            privateMessage.UserColor
                        );
                    }
                });
            },
            cfg => cfg.WithTopic(privateTopic)
        );
    }

    /// <summary>
    /// Sends a public chat message or handles commands like /statistik.
    /// </summary>
    public async void SendMessage(string text)
    {
        text ??= "";
        var trimmed = text.Trim();

        if (string.IsNullOrWhiteSpace(trimmed))
            return;

        // --- Normal message ---
        _bus.PubSub.Publish(new SubmitMessageCommand(Username, text));
    }

    /// <summary>
    /// Sends a private message to another user.
    /// </summary>
    public void SendPrivateMessage(string recipientUsername, string text)
    {
        if (string.Equals(recipientUsername, Username, StringComparison.OrdinalIgnoreCase))
        {
            _appendMessageCallback("[ERROR] You cannot send a private message to yourself.", "red");
            return;
        }

        var command = new SendPrivateMessageCommand(
            Username,
            recipientUsername,
            text
        );

        _bus.PubSub.Publish(command);

    }


    /// <summary>
    /// Sends a request to the server to retrieve the current server time
    /// and displays it in the chat UI. If the request fails, an error
    /// message is appended instead.
    /// </summary>
    /// <returns>A Task representing the asynchronous operation.</returns>
    public async Task HandleTimeCommand()
    {
        try
        {
            var res = await _bus.Rpc.RequestAsync<TimeRequest, TimeResponse>(new TimeRequest());
            _appendMessageCallback($"[INFO] Current server time: {res.CurrentTime}", "Black");
        }
        catch (Exception ex)
        {
            _appendMessageCallback(
                $"[ERROR] Time couldn't be fetched: {ex.Message}", "red"
            );
        }
    }


    /// <summary>
    /// Handles the <c>/statistik</c> command by requesting chat statistics
    /// from the server and printing the result to the chat output.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous statistics handling operation.
    /// </returns>
    public async Task HandleStatisticsCommand()
    {
        try
        {
            var res = await _bus.Rpc.RequestAsync<
                StatisticsRequest,
                StatisticsResponse>(
                    new StatisticsRequest(Username)
                );

            _appendMessageCallback("=== Statistics ===", "Black");
            _appendMessageCallback($"Total Messages: {res.TotalMessages}", "Black");
            _appendMessageCallback($"Ø Messages per User: {res.AvgMessagesPerUser:F2}", "Black");
            _appendMessageCallback("Top 3 most active Chatters:", "Black");

            if (res.Top3 == null || res.Top3.Count == 0)
            {
                _appendMessageCallback("  (currently no data)", "Black");
            }
            else
            {
                int rank = 1;
                foreach (var t in res.Top3)
                {
                    _appendMessageCallback($"  {rank}. {t.User}: {t.Messages}", "Black");
                    rank++;
                }
            }

            _appendMessageCallback("================", "Black");
        }
        catch (Exception ex)
        {
            _appendMessageCallback(
                $"[ERROR] Statistics couldn't be fetched: {ex.Message}", "red"
            );
        }
    }


    /// <summary>
    /// Ensures the sent file exists and is smaller than 1 MB in size. If so, sends the file.
    /// </summary>
    public async Task HandleSendFile(string path)
    {
        if (!File.Exists(path))
        {
            _appendMessageCallback("[ERROR] File does not exist.", "red");
            return;
        }

        var info = new FileInfo(path);
        if (info.Length > 1_000_000)
        {
            _appendMessageCallback("[ERROR] File too large (max 1 MB).", "red");
            return;
        }

        byte[] bytes = await File.ReadAllBytesAsync(path);
        string base64 = Convert.ToBase64String(bytes);

        _bus.PubSub.Publish(
            new SendFileCommand(Username, info.Name, base64, info.Length)
        );

        _appendMessageCallback($"[YOU] Sent file {info.Name}", "BrightGreen");
    }


    /// <summary>
    /// Saves a received file in Downloads/Chat.
    /// </summary>
    public async Task SaveFile(FileReceivedEvent file)
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

        _appendMessageCallback($"[SAVED] {path}", "BrightGreen");
    }


    /// <summary>
    /// Starts sending periodic heartbeat messages to the server to indicate that this client is still online.
    /// The heartbeat is sent every 10 seconds using a timer and Pub/Sub on the bus.
    /// </summary>
    public void StartHeartbeat()
    {
        _heartbeatTimer = new Timer(_ =>
        {
            _bus.PubSub.Publish(new Heartbeat(Username));
        }, null, TimeSpan.Zero, TimeSpan.FromSeconds(10));
    }


    /// <summary>
    /// Stops sending heartbeat messages by disposing the underlying timer.
    /// Call this method when the client is logging out or the application is closing.
    /// </summary>
    public void StopHeartbeat()
    {
        _heartbeatTimer?.Dispose();
    }
}
