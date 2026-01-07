using EasyNetQ;
using Chat.Contracts;
using Chat.Contracts.Infrastructure;
using Terminal.Gui;

namespace ChatClient.Infrastructure;

/// <summary>
/// Manages all Pub/Sub subscriptions for the chat client.
/// Centralizes subscription handlers for broadcasts, private messages, files, notifications, and heartbeats.
/// </summary>
public class SubscriptionManager
{
    private readonly IBus _bus;
    private readonly string _username;
    private readonly UiInvoker _uiInvoker;
    private readonly Func<BroadcastFileEvent, Dialog> _showFileDialog;
    private readonly Action<DateTime> _updateHeartbeat;
    private readonly Action<string, string> _appendMessage;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriptionManager"/> class.
    /// </summary>
    /// <param name="bus">The Pub/Sub bus instance for subscribing to events.</param>
    /// <param name="username">The current user's username for topic filtering.</param>
    /// <param name="uiInvoker">UI invoker for safely updating the chat view.</param>
    /// <param name="showFileDialog">Callback to show a file save dialog when a file is received.</param>
    /// <param name="updateHeartbeat">Callback to update the last server heartbeat timestamp.</param>
    /// <param name="appendMessage">Callback to append a message to the UI chat view.</param>
    public SubscriptionManager(
        IBus bus,
        string username,
        UiInvoker uiInvoker,
        Func<BroadcastFileEvent, Dialog> showFileDialog,
        Action<DateTime> updateHeartbeat,
        Action<string, string> appendMessage)
    {
        _bus = bus;
        _username = username;
        _uiInvoker = uiInvoker;
        _showFileDialog = showFileDialog;
        _updateHeartbeat = updateHeartbeat;
        _appendMessage = appendMessage;
    }

    /// <summary>
    /// Registers all Pub/Sub subscriptions for the chat client.
    /// Subscribes to broadcast messages, notifications, files, private messages, errors, and server heartbeats.
    /// </summary>
    public void SubscribeAll()
    {
        string subscriptionId = $"chat_client_{Guid.NewGuid()}";
        string privateTopic = TopicNames.CreatePrivateUserTopicName(_username);

        // Receives public broadcast chat messages from all users
        SafeSubscribe<BroadcastMessageEvent>(subscriptionId, msg =>
            _uiInvoker.SafeInvoke(() =>
                _appendMessage($"{msg.Username}: {msg.Text}", msg.UserColor))
        );

        // Receives informational system notifications (e.g. user join/leave events)
        SafeSubscribe<UserNotification>(subscriptionId, note =>
            _uiInvoker.SafeInvoke(() =>
                _appendMessage($"[INFO] {note.Text}", "Black"))
        );

        // Receives broadcast file announcements and prompts the user to save the file
        SafeSubscribe<BroadcastFileEvent>(subscriptionId, file =>
        {
            // Ignore files sent by the current user
            if (file.Sender == _username)
                return;

            _uiInvoker.SafeInvoke(() =>
            {
                _appendMessage(
                    $"[FILE] {file.Sender}: {file.FileName} ({file.FileSizeBytes} bytes)",
                    "BrightGreen");

                var dialog = _showFileDialog(file);
                Application.Run(dialog);
            });
        });

        // Receives private messages sent to or from the current user (topic-filtered)
        SafeSubscribe<PrivateMessageEvent>(
            privateTopic,
            msg =>
                _uiInvoker.SafeInvoke(() =>
                {
                    var label = msg.IsOutgoing
                        ? $"[PRIVATE → {msg.RecipientUsername}]"
                        : $"[PRIVATE ← {msg.SenderUsername}]";

                    _appendMessage($"{label} {msg.Text}", msg.UserColor);
                }),
            cfg => cfg.WithTopic(privateTopic)
        );

        // Receives error events scoped to the current user (topic-filtered)
        SafeSubscribe<ErrorEvent>(
            privateTopic,
            error =>
                _uiInvoker.SafeInvoke(() =>
                    _appendMessage($"[ERROR] {error.Message}", "Red")),
            cfg => cfg.WithTopic(privateTopic)
        );

        // Receives periodic server heartbeat events to track server availability
        SafeSubscribe<ServerHeartbeat>(
            $"client_heartbeat_{_username}",
            hb => _updateHeartbeat(hb.Timestamp)
        );
    }



    /// <summary>
    /// Safely registers a Pub/Sub subscription for the specified message type.
    /// </summary>
    /// <typeparam name="T">
    /// The message type to subscribe to.
    /// </typeparam>
    /// <param name="subscriptionId">
    /// The EasyNetQ subscription identifier.
    /// </param>
    /// <param name="handler">
    /// The message handler invoked for each received message.
    /// The handler is expected to route all UI-related logic through <see cref="UiInvoker.SafeInvoke"/>.
    /// </param>
    /// <param name="configure">
    /// Optional subscription configuration callback (e.g. topic filtering).
    /// </param>
    private void SafeSubscribe<T>(
        string subscriptionId,
        Action<T> handler,
        Action<ISubscriptionConfiguration>? configure = null)
    {
        try
        {
            _bus.PubSub.Subscribe<T>(
                subscriptionId,
                message => handler(message),
                cfg => configure?.Invoke(cfg));
        }
        catch (Exception)
        {
            _uiInvoker.SafeInvoke(() => _appendMessage($"[ERROR] Couldn't subscribe to {typeof(T).Name}.", "Red"));
        }
    }
}
