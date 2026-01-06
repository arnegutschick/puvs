namespace ChatClient.Infrastructure;

/// <summary>
/// Centralizes error handling for Pub/Sub publish operations.
/// Wraps publish calls with try/catch to prevent client crashes from connection failures.
/// </summary>
public class BusPublisher
{
    private readonly Action<string, string> _appendMessage;
    private readonly Action _onConnectionProblem;

    /// <summary>
    /// Initializes a new instance of the <see cref="BusPublisher"/> class.
    /// </summary>
    /// <param name="appendMessage">Callback to append an error message to the UI.</param>
    /// <param name="onConnectionProblem">Callback invoked when a publish operation fails.</param>
    public BusPublisher(Action<string, string> appendMessage, Action onConnectionProblem)
    {
        _appendMessage = appendMessage;
        _onConnectionProblem = onConnectionProblem;
    }

    /// <summary>
    /// Attempts to publish a message on the bus with error handling.
    /// On exception, reports the error and triggers connection problem handling.
    /// </summary>
    /// <param name="publishAction">The publish action to execute (typically a Pub/Sub.Publish call).</param>
    public async Task TryPublishAsync(Func<Task> publishAction)
    {
        try
        {
            await publishAction();
        }
        catch (Exception)
        {
            _onConnectionProblem?.Invoke();
            _appendMessage?.Invoke("[ERROR] Connection problem. Maybe RabbitMQ is down?", "Red");
        }
    }
}
