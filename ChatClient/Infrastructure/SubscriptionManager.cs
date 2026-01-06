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

        _bus.PubSub.Subscribe<BroadcastMessageEvent>(subscriptionId, msg =>
        {
            _uiInvoker.SafeInvoke(() => _appendMessage($"{msg.Username}: {msg.Text}", msg.UserColor));
        });

        _bus.PubSub.Subscribe<UserNotification>(subscriptionId, note =>
        {
            _uiInvoker.SafeInvoke(() => _appendMessage($"[INFO] {note.Text}", "Black"));
        });

        _bus.PubSub.Subscribe<BroadcastFileEvent>(subscriptionId, file =>
        {
            if (file.Sender == _username) return;

            _uiInvoker.SafeInvoke(() =>
            {
                _appendMessage($"[FILE] {file.Sender}: {file.FileName} ({file.FileSizeBytes} bytes)", "BrightGreen");

                var dialog = _showFileDialog(file);
                Application.Run(dialog);
            });
        });

        _bus.PubSub.Subscribe<PrivateMessageEvent>(
            privateTopic,
            privateMessage =>
            {
                _uiInvoker.SafeInvoke(() =>
                {
                    if (privateMessage.IsOutgoing)
                    {
                        _appendMessage($"[PRIVATE → {privateMessage.RecipientUsername}] {privateMessage.Text}", privateMessage.UserColor);
                    }
                    else
                    {
                        _appendMessage($"[PRIVATE ← {privateMessage.SenderUsername}] {privateMessage.Text}", privateMessage.UserColor);
                    }
                });
            },
            cfg => cfg.WithTopic(privateTopic)
        );

        _bus.PubSub.Subscribe<ErrorEvent>(
            privateTopic,
            error => _uiInvoker.SafeInvoke(() => _appendMessage($"[ERROR] {error.Message}", "Red")),
            cfg => cfg.WithTopic(privateTopic)
        );

        _bus.PubSub.Subscribe<ServerHeartbeat>($"client_heartbeat_{_username}", hb => _updateHeartbeat(hb.Timestamp));
    }
}
