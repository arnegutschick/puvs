using EasyNetQ;
using Chat.Contracts;
using Chat.Contracts.Infrastructure;
using Terminal.Gui;
using ChatClient.Infrastructure;
using ChatClient.Services;
using Microsoft.Extensions.Configuration;
using System.Runtime.CompilerServices;

namespace ChatClient;

/// <summary>
/// Orchestrates the core chat client logic by delegating to specialized services.
/// Acts as a facade that coordinates message sending, file transfers, heartbeat management,
/// and Pub/Sub event subscriptions through dependency-injected services.
/// </summary>
/// <remarks>
/// This class provides a simple public API for the UI layer, internally delegating to:
/// <list type="bullet">
/// <item><see cref="MessageService"/> for broadcast and RPC-based commands</item>
/// <item><see cref="PrivateMessageService"/> for private messaging</item>
/// <item><see cref="FileService"/> for file send/receive</item>
/// <item><see cref="HeartbeatService"/> for server presence</item>
/// <item><see cref="SubscriptionManager"/> for Pub/Sub event handling</item>
/// <item><see cref="UiInvoker"/> for safe UI updates</item>
/// <item><see cref="BusPublisher"/> for error-safe publish operations</item>
/// </list>
/// All methods preserve their original signatures for backward compatibility with the UI layer.
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
    // Current user's username
    internal string Username { get; }
    // Last registered heartbeat from the server 
    private DateTime _lastServerHeartbeat = DateTime.UtcNow;
    private readonly UiInvoker _uiInvoker;
    private readonly BusPublisher _busPublisher;
    private readonly MessageService _messageService;
    private readonly PrivateMessageService _privateMessageService;
    private readonly FileService _fileService;
    private readonly HeartbeatService _heartbeatService;

    // Calculates if the server is still online by comparing latest heartbeat with timeout value
    private bool ServerReachable =>
        (DateTime.UtcNow - _lastServerHeartbeat) <
        TimeSpan.FromSeconds(_configuration.GetValue("ChatSettings:HeartbeatTimeoutSeconds", 30));

    // Constructor
    /// <summary>
    /// Initializes a new instance of the <see cref="ChatLogic"/> class with all required dependencies.
    /// </summary>
    /// <param name="bus">The Pub/Sub bus for message publishing and RPC requests.</param>
    /// <param name="username">The current user's username.</param>
    /// <param name="configuration">Configuration containing heartbeat and server reachability settings.</param>
    /// <param name="appendMessageCallback">Callback to append messages to the UI chat view.</param>
    /// <param name="showFileDialogCallback">Callback to show a file save dialog when files are received.</param>
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
        _uiInvoker = new UiInvoker(_appendMessageCallback, HandleBusError);
        _busPublisher = new BusPublisher(_appendMessageCallback, HandleBusError);

        // instantiate services
        _messageService = new MessageService(_bus, _busPublisher, _appendMessageCallback, Username);
        _privateMessageService = new PrivateMessageService(_bus, _busPublisher, _appendMessageCallback, Username);
        _fileService = new FileService(_bus, _busPublisher, _appendMessageCallback, Username);
        _heartbeatService = new HeartbeatService(_bus, _configuration, _busPublisher, Username);
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
        // delegate subscriptions to SubscriptionManager
        var subscriptionManager = new SubscriptionManager(
            _bus,
            Username,
            _uiInvoker,
            _showFileDialogCallback,
            ts => _lastServerHeartbeat = ts,
            _appendMessageCallback
        );

        subscriptionManager.SubscribeAll();
    }

    /// <summary>
    /// Sends a public message to all users via the Pub/Sub system.
    /// Ignores empty or whitespace-only messages.
    /// </summary>
    /// <param name="text">The message text to send.</param>
    public async Task SendMessageAsync(string text)
    {
        await _messageService.SendMessageAsync(text);
    }

    /// <summary>
    /// Sends a private message to a specific user via the Pub/Sub system.
    /// Validates that the recipient is not the current user before sending.
    /// </summary>
    /// <param name="recipientUsername">The username of the message recipient.</param>
    /// <param name="text">The message content to send.</param>
    public async Task SendPrivateMessageAsync(string recipientUsername, string text)
    {
        await _privateMessageService.SendPrivateMessageAsync(recipientUsername, text);
    }

    /// <summary>
    /// Handles the /time command: requests the current server time via RPC
    /// and displays it in the chat message view.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task HandleTimeCommandAsync()
    {
        await _messageService.HandleTimeCommandAsync();
    }

    /// <summary>
    /// Handles the /users command: requests the list of currently logged in users via RPC
    /// and displays it in the chat message view.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task HandleUsersCommandAsync()
    {
        await _messageService.HandleUsersCommandAsync();
    }

    /// <summary>
    /// Handles the /stats command: requests chat statistics from the server
    /// and displays them line by line in the message view.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task HandleStatisticsCommandAsync()
    {
        await _messageService.HandleStatisticsCommandAsync();
    }

    /// <summary>
    /// Handles sending a file to other users via the chat's Pub/Sub system.
    /// Checks file existence and size, reads and encodes it, then publishes a <see cref="SendFileCommand"/>.
    /// </summary>
    /// <param name="path">The local path of the file to send.</param>
    /// <returns>A task representing the asynchronous send operation.</returns>
    public async Task HandleSendFileAsync(string path)
    {
        await _fileService.HandleSendFileAsync(path);
    }

    /// <summary>
    /// Saves a received file from a <see cref="BroadcastFileEvent"/> to the user's Downloads/Chat folder.
    /// Decodes the Base64 content and writes it to disk asynchronously.
    /// </summary>
    /// <param name="file">The broadcast file event containing the file name and Base64-encoded content.</param>
    /// <returns>A task representing the asynchronous save operation.</returns>
    public async Task SaveFileAsync(BroadcastFileEvent file)
    {
        await _fileService.SaveFileAsync(file);
    }
    /// <summary>
    /// Starts sending periodic heartbeat messages to the server to indicate that this client is still online.
    /// The heartbeat is sent using a timer and Pub/Sub on the bus. The time interval can be modified in the 'appsettings.json' file
    /// </summary>
    public async Task StartClientHeartbeat()
    {
        await _heartbeatService.Start();
    }

    /// <summary>
    /// Stops sending heartbeat messages by disposing the underlying timer.
    /// Call this method when the client is logging out or the application is closing.
    /// </summary>
    public void StopClientHeartbeat()
    {
        _heartbeatService.Stop();
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
