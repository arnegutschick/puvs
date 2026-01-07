using Chat.Contracts;
using EasyNetQ;

namespace ChatClient.Services;

/// <summary>
/// Handles user login and logout interactions with the server via RPC and Pub/Sub.
/// Encapsulates the RPC login request and the logout publish so callers (e.g. `Program`) can remain simple.
/// </summary>
public class LoginService
{
    private readonly IBus _bus;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoginService"/> class.
    /// </summary>
    /// <param name="bus">The EasyNetQ bus used for RPC and Pub/Sub operations.</param>
    public LoginService(IBus bus)
    {
        _bus = bus;
    }

    /// <summary>
    /// Performs an RPC login request to the server using the provided username.
    /// </summary>
    /// <param name="username">The username to log in with.</param>
    /// <returns>
    /// The server <see cref="LoginResponse"/> if the RPC call succeeds; otherwise <c>null</c> when a communication error occurs.
    /// </returns>
    public async Task<LoginResponse?> LoginAsync(string username)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var requestTask = _bus.Rpc.RequestAsync<LoginRequest, LoginResponse>(new LoginRequest(username));
            var res = await requestTask.WaitAsync(cts.Token);
            return res;
        }
        catch (Exception)
        {
            Console.WriteLine("[FATAL] Login request failed. Maybe RabbitMQ is down?");
            return null;
        }
    }

    /// <summary>
    /// Publishes a logout request to notify the server that this client is exiting.
    /// This method swallows publish exceptions but reports an error message when provided with a callback.
    /// Uses a 1-second timeout to prevent blocking the main thread if RabbitMQ is down.
    /// </summary>
    /// <param name="username">The username that is logging out.</param>
    public async Task LogoutAsync(string username)
    {
        try
        {
            // Use a timeout to prevent blocking the main thread if RabbitMQ is unreachable
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var requestTask = _bus.PubSub.PublishAsync(new LogoutRequest(username));
            await requestTask.WaitAsync(cts.Token);
        }
        catch (Exception)
        {
            // Silently swallow errors during logout (RabbitMQ down, timeout, etc.)
            // Don't report to console as the application is already shutting down
        }
    }
}
