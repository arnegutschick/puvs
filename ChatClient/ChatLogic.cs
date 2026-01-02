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

    // Constructor
    public ChatLogic(
        IBus bus,
        string username,
        IConfiguration configuration,
        Action<string, string> appendMessageCallback,
        Func<BroadcastFileEvent, Dialog> showFileDialogCallback
        )
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
        _bus.PubSub.Subscribe<BroadcastFileEvent>(subscriptionId, file =>
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

        // --- Error Messages ---
        _bus.PubSub.Subscribe<ErrorEvent>(
            privateTopic,
            error =>
            {
                Application.MainLoop.Invoke(() =>
                {
                    _appendMessageCallback(
                        $"[ERROR] {error.Message}",
                        "Red"
                    );
                });
            },
            cfg => cfg.WithTopic(privateTopic)
        );
    }


    /// <summary>
    /// Sends a public message to all users via the Pub/Sub system.
    /// Ignores empty or whitespace-only messages.
    /// </summary>
    /// <param name="text">The message text to send.</param>
    public async void SendMessage(string text)
    {
        // Ensure text is not null
        text ??= "";

        var trimmed = text.Trim();

        // Ignore empty messages
        if (string.IsNullOrWhiteSpace(trimmed))
            return;

        // Publish the message to all users
        _bus.PubSub.Publish(new SubmitMessageCommand(Username, text));
    }


    /// <summary>
    /// Sends a private message to a specific user via the Pub/Sub system.
    /// Validates that the recipient is not the current user before sending.
    /// </summary>
    /// <param name="recipientUsername">The username of the message recipient.</param>
    /// <param name="text">The message content to send.</param>
    public void SendPrivateMessage(string recipientUsername, string text)
    {
        // Prevent sending a private message to oneself
        if (string.Equals(recipientUsername, Username, StringComparison.OrdinalIgnoreCase))
        {
            _appendMessageCallback("[ERROR] You cannot send a private message to yourself.", "red");
            return;
        }

        // Create a command representing the private message
        var command = new SendPrivateMessageCommand(
            Username,
            recipientUsername,
            text
        );

        // Publish the command via the Pub/Sub system
        _bus.PubSub.Publish(command);
    }


    /// <summary>
    /// Handles the /time command: requests the current server time via RPC
    /// and displays it in the chat message view.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task HandleTimeCommand()
    {
        // Request current server time via RPC
        var res = await _bus.Rpc.RequestAsync<TimeRequest, TimeResponse>(
            new TimeRequest(Username)
        );
        if (res.IsSuccess)
        {
            // Display the server time in the chat
            _appendMessageCallback($"[INFO] Current server time: {res.CurrentTime}", "Black");
        }
    }


    /// <summary>
    /// Handles the /users command: requests the list of currently logged in users via RPC
    /// and displays it in the chat message view.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task HandleUsersCommand()
    {
        // Request current server time via RPC
        var res = await _bus.Rpc.RequestAsync<UserListRequest, UserListResponse>(
            new UserListRequest(Username)
        );
        if (res.IsSuccess)
        {
            // Display a list of all currently logged in members
            _appendMessageCallback($"=== Currently logged in users ===", "Black");

            foreach (string user in res.UserList)
            {
                _appendMessageCallback($"- {user}", "Black");
            }

            _appendMessageCallback("==================================", "Black");
        }
    }



    /// <summary>
    /// Handles the /stats command: requests chat statistics from the server
    /// and displays them line by line in the message view.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task HandleStatisticsCommand()
    {

        // Request statistics from the server via RPC
        var res = await _bus.Rpc.RequestAsync<
            StatisticsRequest,
            StatisticsResponse>(
                new StatisticsRequest(Username)
            );

        if (res.IsSuccess)
        {
            // Display statistics
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


    /// <summary>
    /// Handles sending a file to other users via the chat's Pub/Sub system.
    /// Checks file existence and size, reads and encodes it, then publishes a <see cref="SendFileCommand"/>.
    /// </summary>
    /// <param name="path">The local path of the file to send.</param>
    /// <returns>A task representing the asynchronous send operation.</returns>
    public async Task HandleSendFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("[ERROR] Invalid file path.");
            return;
        }
        
        // Check if the file exists
        if (!File.Exists(path))
        {
            _appendMessageCallback("[ERROR] File does not exist.", "red");
            return;
        }

        var info = new FileInfo(path);

        // Check if the file exceeds 1 MB
        if (info.Length > 1_000_000)
        {
            _appendMessageCallback("[ERROR] File is too large (max 1 MB).", "red");
            return;
        }

        // Read file bytes asynchronously
        byte[] bytes = await File.ReadAllBytesAsync(path);

        // Convert file content to Base64 string for transmission
        string base64 = Convert.ToBase64String(bytes);

        // Publish the file to other users via the Pub/Sub system
        _bus.PubSub.Publish(
            new SendFileCommand(Username, info.Name, base64, info.Length)
        );

        // Notify the user that the file has been sent
        _appendMessageCallback($"[YOU] Sent file {info.Name}", "BrightGreen");
    }


    /// <summary>
    /// Saves a received file from a <see cref="BroadcastFileEvent"/> to the user's Downloads/Chat folder.
    /// Decodes the Base64 content and writes it to disk asynchronously.
    /// </summary>
    /// <param name="file">The broadcast file event containing the file name and Base64-encoded content.</param>
    /// <returns>A task representing the asynchronous save operation.</returns>
    public async Task SaveFile(BroadcastFileEvent file)
    {
        // Decode the Base64 content into a byte array
        byte[] data = Convert.FromBase64String(file.ContentBase64);

        // Determine the save directory: ~/Downloads/Chat
        string dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads",
            "Chat"
        );

        // Ensure the directory exists
        Directory.CreateDirectory(dir);

        // Full path to save the file
        string path = Path.Combine(dir, file.FileName);

        // Write the file asynchronously
        await File.WriteAllBytesAsync(path, data);

        // Notify the user in the chat that the file has been saved
        _appendMessageCallback($"[SAVED] {path}", "BrightGreen");
    }


    /// <summary>
    /// Starts sending periodic heartbeat messages to the server to indicate that this client is still online.
    /// The heartbeat is sent using a timer and Pub/Sub on the bus. The time interval can be modified in the 'appsettings.json' file
    /// </summary>
    public void StartHeartbeat()
    {
        // Retrieve heartbeat interval from config file
        int intervalSeconds = _configuration.GetValue("ChatSettings:ClientHeartbeatIntervalSeconds", 10);

        // Start a new timer that sends a heartbeat to the server 
        _heartbeatTimer = new Timer(_ =>
        {
            _bus.PubSub.Publish(new Heartbeat(Username));
        }, null, TimeSpan.Zero, TimeSpan.FromSeconds(intervalSeconds));
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
