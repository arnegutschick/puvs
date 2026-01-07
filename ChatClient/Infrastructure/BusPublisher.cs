using Terminal.Gui;

namespace ChatClient.Infrastructure;

/// <summary>
/// Centralizes error handling for Pub/Sub publish operations.
/// Wraps publish calls with try/catch to prevent client crashes from connection failures.
/// </summary>
public class BusPublisher
{
    private readonly Action<string, string> _appendMessage;
    private readonly Action _onConnectionProblem;
    private readonly Func<bool> _isServerReachable;

    /// <summary>
    /// Initializes a new instance of the <see cref="BusPublisher"/> class.
    /// </summary>
    /// <param name="appendMessage">Callback to append an error message to the UI.</param>
    /// <param name="onConnectionProblem">Callback invoked when a publish operation fails.</param>
    /// <param name="isServerReachable">
    /// Delegate that returns <c>true</c> if the chat server and message bus
    /// are currently considered reachable, or <c>false</c> otherwise.
    /// This is used as a precondition check before attempting publish operations
    /// to avoid unnecessary failures when the server or RabbitMQ are offline.
    /// </param>
    public BusPublisher(Action<string, string> appendMessage, Action onConnectionProblem, Func<bool> isServerReachable)
    {
        _appendMessage = appendMessage;
        _onConnectionProblem = onConnectionProblem;
        _isServerReachable = isServerReachable;
    }

    /// <summary>
    /// Attempts to publish a message on the bus with error handling and timeout protection.
    /// Uses a cancellation token with a 5-second timeout to prevent blocking indefinitely when RabbitMQ is down.
    /// On exception, reports the error and triggers connection problem handling.
    /// </summary>
    /// <param name="publishAction">The publish action to execute (typically a Pub/Sub.Publish call).</param>
    /// <param name="isHeartbeat">
    /// Indicates whether the publish operation is a background heartbeat message.
    /// When set to <c>true</c>, publish failures are handled silently in the background
    /// (no user-facing chat error messages are shown), while connection state callbacks
    /// are still invoked. When <c>false</c>, publish failures are reported to the user.
    /// </param>
    public async Task TryPublishAsync(Func<Task> publishAction, bool isHeartbeat = false)
    {
        if (!_isServerReachable())
        {
            if (!isHeartbeat)
            {
                Application.MainLoop.Invoke(() =>
                {
                    _appendMessage("[ERROR] Either the server or RabbitMQ are offline.", "Red");
                });
            }
            return;
        }

        try
        {
            // Use a cancellation token to enforce a timeout and properly cancel the operation in case RabbitMQ is down
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var publishTask = publishAction();

            // Pass the token to the task so it can be properly cancelled
            await publishTask.WaitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            _onConnectionProblem?.Invoke();

            if (!isHeartbeat)
            {
                Application.MainLoop.Invoke(() =>
                {
                    _appendMessage("[ERROR] Connection problem. Maybe RabbitMQ is down?", "Red");
                });
            }
        }
        catch (Exception)
        {
            _onConnectionProblem?.Invoke();

            if (!isHeartbeat)
            {
                Application.MainLoop.Invoke(() =>
                {
                    _appendMessage("[ERROR] Connection problem. Maybe RabbitMQ is down?", "Red");
                });
            }
        }
    }
}
