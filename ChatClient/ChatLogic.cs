using EasyNetQ;
using Chat.Contracts;
using Chat.Contracts.Infrastructure;
using Terminal.Gui;
using Microsoft.Extensions.Configuration;

namespace ChatClient;

/// <summary>
/// Encapsulates the core logic for the chat client.
/// Handles sending and receiving messages, file transfers, private messages,
/// user notifications, and periodic heartbeat messages.
/// </summary>
/// <remarks>
/// This class interacts with a Pub/Sub bus (<see cref="IBus"/>)
/// and updates the user interface via provided callbacks.
/// It also manages commands such as /stats, /time, /sendfile, and private messaging.
/// </remarks>
public class ChatLogic
{
    // Pub / Sub for sending and receiving messages
    private readonly IBus _bus;
    // Callback to append messages to the UI
    private readonly Action<string, string> _appendMessageCallback;
    // Callback to open a file save dialog when receiving a file event
    private readonly Func<BroadcastFileEvent, Dialog> _showFileDialogCallback;
    // Configuration settings
    private readonly IConfiguration _configuration;
    // Timer for heartbeat messages
    private Timer? _heartbeatTimer;
    // Current user's username
    internal string Username { get; }
    // Last registered heartbeat from the server 
    private DateTime _lastServerHeartbeat = DateTime.UtcNow;

    // Calculates if the server is still online by comparing latest heartbeat with timeout value
    private bool ServerReachable =>
        (DateTime.UtcNow - _lastServerHeartbeat) <
        TimeSpan.FromSeconds(_configuration.GetValue("ChatSettings:HeartbeatTimeoutSeconds", 30));

    // Constructor
    public ChatLogic(
        IBus bus,
        string username,
        IConfiguration configuration,
        Action<string, string> appendMessageCallback,
        Func<BroadcastFileEvent, Dialog> showFileDialogCallback)
    {
        _bus = bus;
        Username = username;
        _configuration = configuration;
        _appendMessageCallback = appendMessageCallback;
        _showFileDialogCallback = showFileDialogCallback;
    }

    /// <summary>
    /// Subscribes to all relevant Pub/Sub events for the chat client.
    /// Handles incoming broadcast messages, private messages, user notifications,
    /// file messages, and error events, updating the UI appropriately.
    /// </summary>
    /// <remarks>
    /// The subscriptions include:
    /// <list type="bullet">
    /// <item>Broadcast chat messages (<see cref="BroadcastMessageEvent"/>)</item>
    /// <item>User notifications (<see cref="UserNotification"/>)</item>
    /// <item>File messages (<see cref="BroadcastFileEvent"/>) with a save dialog</item>
    /// <item>Private messages (<see cref="PrivateMessageEvent"/>) for the current user</item>
    /// <item>Error messages (<see cref="ErrorEvent"/>)</item>
    /// </list>
    /// All UI updates are invoked on the main application loop.
    /// </remarks>
    public void SubscribeEvents()
    {
        string subscriptionId = $"chat_client_{Guid.NewGuid()}";
        string privateTopic = TopicNames.CreatePrivateUserTopicName(Username);

        // --- Broadcast chat messages ---
        _bus.PubSub.Subscribe<BroadcastMessageEvent>(subscriptionId, msg =>
        {
            try
            {
                Application.MainLoop.Invoke(() =>
                {
                    _appendMessageCallback($"{msg.Username}: {msg.Text}", msg.UserColor);
                });
            }
            catch (Exception)
            {
                HandleBusError();
            }
        });

        // --- User notifications ---
        _bus.PubSub.Subscribe<UserNotification>(subscriptionId, note =>
        {
            try
            {
                Application.MainLoop.Invoke(() =>
                {
                    _appendMessageCallback($"[INFO] {note.Text}", "Black");
                });
            }
            catch (Exception)
            {
                HandleBusError();
            }
        });

        // --- File messages ---
        _bus.PubSub.Subscribe<BroadcastFileEvent>(subscriptionId, file =>
        {
            if (file.Sender == Username) return;

            try
            {
                Application.MainLoop.Invoke(() =>
                {
                    _appendMessageCallback(
                        $"[FILE] {file.Sender}: {file.FileName} ({file.FileSizeBytes} bytes)",
                        "BrightGreen"
                    );

                    var dialog = _showFileDialogCallback(file);
                    Application.Run(dialog);
                });
            }
            catch (Exception)
            {
                HandleBusError();
            }
        });

        // --- Private messages (topic-based) ---
        _bus.PubSub.Subscribe<PrivateMessageEvent>(
            privateTopic,
            privateMessage =>
            {
                try
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
                }
                catch (Exception)
                {
                    HandleBusError();
                }
            },
            cfg => cfg.WithTopic(privateTopic)
        );

        // --- Error Messages ---
        _bus.PubSub.Subscribe<ErrorEvent>(
            privateTopic,
            error =>
            {
                Application.MainLoop.Invoke(() =>
                {
                    _appendMessageCallback($"[ERROR] {error.Message}", "Red");
                });
            },
            cfg => cfg.WithTopic(privateTopic)
        );

        // --- Server Heartbeat ---
        _bus.PubSub.Subscribe<ServerHeartbeat>(
            $"client_heartbeat_{Username}",
            hb =>
            {
                _lastServerHeartbeat = hb.Timestamp;
            });
    }

    /// <summary>
    /// Sends a public message to all users via the Pub/Sub system.
    /// Ignores empty or whitespace-only messages.
    /// </summary>
    /// <param name="text">The message text to send.</param>
    public async Task SendMessageAsync(string text)
    {
        text ??= "";
        var trimmed = text.Trim();

        if (string.IsNullOrWhiteSpace(trimmed))
            return;

        if (!EnsureServerReachable()) return;

        try
        {
            _bus.PubSub.Publish(new SubmitMessageCommand(Username, text));
        }
        catch (Exception)
        {
            HandleBusError();
        }
    }

    /// <summary>
    /// Sends a private message to a specific user via the Pub/Sub system.
    /// Validates that the recipient is not the current user before sending.
    /// </summary>
    /// <param name="recipientUsername">The username of the message recipient.</param>
    /// <param name="text">The message content to send.</param>
    public async Task SendPrivateMessageAsync(string recipientUsername, string text)
    {
        if (string.Equals(recipientUsername, Username, StringComparison.OrdinalIgnoreCase))
        {
            _appendMessageCallback("[ERROR] You cannot send a private message to yourself.", "Red");
            return;
        }

        if (!EnsureServerReachable()) return;

        try
        {
            _bus.PubSub.Publish(
                new SendPrivateMessageCommand(Username, recipientUsername, text)
            );
        }
        catch (Exception)
        {
            HandleBusError();
        }
    }

    /// <summary>
    /// Handles the /time command: requests the current server time via RPC
    /// and displays it in the chat message view.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task HandleTimeCommandAsync()
    {
        if (!EnsureServerReachable()) return;

        try
        {
            var res = await _bus.Rpc.RequestAsync<TimeRequest, TimeResponse>(
                new TimeRequest(Username)
            );

            if (res.IsSuccess)
            {
                _appendMessageCallback(
                    $"[INFO] Current server time: {res.CurrentTime}",
                    "Black"
                );
            }
        }
        catch (Exception)
        {
            HandleBusError();
        }
    }

    /// <summary>
    /// Handles the /users command: requests the list of currently logged in users via RPC
    /// and displays it in the chat message view.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task HandleUsersCommandAsync()
    {
        if (!EnsureServerReachable()) return;

        try
        {
            var res = await _bus.Rpc.RequestAsync<UserListRequest, UserListResponse>(
                new UserListRequest(Username)
            );

            if (res.IsSuccess)
            {
                _appendMessageCallback($"=== Currently logged in users ===", "Black");

                foreach (string user in res.UserList)
                {
                    _appendMessageCallback($"- {user}", "Black");
                }

                _appendMessageCallback($"==================================", "Black");
            }
        }
        catch (Exception)
        {
            HandleBusError();
        }
    }

    /// <summary>
    /// Handles the /stats command: requests chat statistics from the server
    /// and displays them line by line in the message view.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task HandleStatisticsCommandAsync()
    {
        if (!EnsureServerReachable()) return;

        try
        {
            var res = await _bus.Rpc.RequestAsync<StatisticsRequest, StatisticsResponse>(
                new StatisticsRequest(Username)
            );

            if (res.IsSuccess)
            {
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
                        _appendMessageCallback($"  {rank}. {t.User}: {t.MessageCount}", "Black");
                        rank++;
                    }
                }

                _appendMessageCallback("=================", "Black");
            }
        }
        catch (Exception)
        {
            HandleBusError();
        }
    }

    /// <summary>
    /// Handles sending a file to other users via the chat's Pub/Sub system.
    /// Checks file existence and size, reads and encodes it, then publishes a <see cref="SendFileCommand"/>.
    /// </summary>
    /// <param name="path">The local path of the file to send.</param>
    /// <returns>A task representing the asynchronous send operation.</returns>
    public async Task HandleSendFileAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            _appendMessageCallback("[ERROR] Invalid file path.", "Red");
            return;
        }

        if (!File.Exists(path))
        {
            _appendMessageCallback("[ERROR] File does not exist.", "Red");
            return;
        }

        var info = new FileInfo(path);

        if (info.Length > 1_000_000)
        {
            _appendMessageCallback("[ERROR] File is too large (max 1 MB).", "Red");
            return;
        }

        if (!EnsureServerReachable()) return;

        try
        {
            byte[] bytes = await File.ReadAllBytesAsync(path);
            string base64 = Convert.ToBase64String(bytes);

            _bus.PubSub.Publish(new SendFileCommand(Username, info.Name, base64, info.Length));

            _appendMessageCallback($"[YOU] Sent file {info.Name}", "BrightGreen");
        }
        catch (Exception)
        {
            HandleBusError();
        }
    }

    /// <summary>
    /// Saves a received file from a <see cref="BroadcastFileEvent"/> to the user's Downloads/Chat folder.
    /// Decodes the Base64 content and writes it to disk asynchronously.
    /// </summary>
    /// <param name="file">The broadcast file event containing the file name and Base64-encoded content.</param>
    /// <returns>A task representing the asynchronous save operation.</returns>
    public async Task SaveFileAsync(BroadcastFileEvent file)
    {
        try
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
        catch (Exception)
        {
            HandleBusError();
        }
    }

    /// <summary>
    /// Starts sending periodic heartbeat messages to the server to indicate that this client is still online.
    /// The heartbeat is sent using a timer and Pub/Sub on the bus. The time interval can be modified in the 'appsettings.json' file
    /// </summary>
    public void StartClientHeartbeat()
    {
        int intervalSeconds =
            _configuration.GetValue("ChatSettings:ClientHeartbeatIntervalSeconds", 10);

        _heartbeatTimer = new Timer(_ =>
        {
            try
            {
                _bus.PubSub.Publish(new ClientHeatbeat(Username));
            }
            catch (Exception) {}
        }, null, TimeSpan.Zero, TimeSpan.FromSeconds(intervalSeconds));
    }

    /// <summary>
    /// Stops sending heartbeat messages by disposing the underlying timer.
    /// Call this method when the client is logging out or the application is closing.
    /// </summary>
    public void StopClientHeartbeat()
    {
        _heartbeatTimer?.Dispose();
    }

    /// <summary>
    /// Checks whether the server is considered online based on the last received heartbeat.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the server is online and requests can be sent; 
    /// <c>false</c> if the server is offline, in which case a message is appended to the UI.
    /// </returns>
    /// <remarks>
    /// This method uses the <see cref="ServerReachable"/> property to determine the server status.
    /// If the server is offline, it notifies the user via the UI callback and blocks further requests.
    /// </remarks>
    private bool EnsureServerReachable()
    {
        if (!ServerReachable)
        {
            _appendMessageCallback("[ERROR] Either the server or RabbitMQ are offline.", "Red");
            return false;
        }
        return true;
    }

    /// <summary>
    /// Centralized error handling for messaging- and UI-related failures.
    /// Prevents client crashes caused by RabbitMQ disconnects or UI exceptions.
    /// </summary>
    private void HandleBusError()
    {
        _lastServerHeartbeat = DateTime.MinValue;

        Application.MainLoop.Invoke(() =>
        {
            _appendMessageCallback(
                $"[ERROR] Connection problem. Maybe RabbitMQ is down?",
                "Red"
            );
        });
    }
}
