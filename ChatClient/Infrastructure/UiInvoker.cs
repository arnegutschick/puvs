using Terminal.Gui;

namespace ChatClient.Infrastructure;

/// <summary>
/// Centralizes safe UI invocation on the Terminal.Gui main loop with error handling.
/// Prevents client crashes caused by UI or connection exceptions.
/// </summary>
public class UiInvoker
{
    private readonly Action<string, string> _appendMessage;
    private readonly Action _onConnectionProblem;

    /// <summary>
    /// Initializes a new instance of the <see cref="UiInvoker"/> class.
    /// </summary>
    /// <param name="appendMessage">Callback to append a message to the UI chat view.</param>
    /// <param name="onConnectionProblem">Callback invoked when a UI or connection error occurs.</param>
    public UiInvoker(Action<string, string> appendMessage, Action onConnectionProblem)
    {
        _appendMessage = appendMessage;
        _onConnectionProblem = onConnectionProblem;
    }

    /// <summary>
    /// Safely invokes an action on the Terminal.Gui main loop with error handling.
    /// On exception, reports the error and triggers connection problem handling.
    /// </summary>
    /// <param name="action">The action to invoke on the main UI loop.</param>
    public void SafeInvoke(Action action)
    {
        try
        {
            Application.MainLoop.Invoke(action);
        }
        catch (Exception)
        {
            _onConnectionProblem?.Invoke();
            _appendMessage?.Invoke("[ERROR] Connection problem. Maybe RabbitMQ is down?", "Red");
        }
    }
}
