using Chat.Contracts;
using EasyNetQ;
using Microsoft.Extensions.Configuration;

namespace ChatServer.Services;

/// <summary>
/// Service responsible for managing user heartbeats and detecting timed-out users.
/// Periodically removes inactive users and publishes notifications to all clients.
/// </summary>
public class HeartbeatService
{
    private readonly UserService _users;
    private readonly IBus _bus;
    private readonly TimeSpan _timeout;

    /// <summary>
    /// Initializes the HeartbeatService.
    /// </summary>
    /// <param name="users">Reference to the UserService for updating and querying users.</param>
    /// <param name="bus">The message bus for publishing timeout notifications.</param>
    /// <param name="configuration">Application configuration for heartbeat timeout settings.</param>
    public HeartbeatService(UserService users, IBus bus, IConfiguration configuration)
    {
        _users = users;
        _bus = bus;

        // Get heartbeat timeout from configuration, default to 30 seconds
        int timeoutSeconds = configuration.GetValue("ChatSettings:HeartbeatTimeoutSeconds", 30);
        _timeout = TimeSpan.FromSeconds(timeoutSeconds);
    }


    /// <summary>
    /// Updates the heartbeat timestamp for a specific user.
    /// Should be called whenever a user sends a message or performs any activity.
    /// </summary>
    /// <param name="username">The username of the active user.</param>
    public void Handle(string username)
    {
        _users.UpdateHeartbeat(username);
    }


    /// <summary>
    /// Starts a background task that periodically checks for timed-out users.
    /// Removes timed-out users and publishes <see cref="UserNotification"/> events.
    /// </summary>
    /// <param name="token">Optional cancellation token to stop the cleanup loop.</param>
    /// <returns>A Task representing the background cleanup loop.</returns>
    public Task StartCleanupTask(CancellationToken token = default)
    {
        return Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                // Wait for the configured heartbeat timeout interval
                await Task.Delay(_timeout, token);

                // Get all users who have not updated their heartbeat within the timeout
                var timedOut = _users.GetTimedOut(_timeout);

                foreach (var username in timedOut)
                {
                    // Remove timed-out users
                    if (_users.Remove(username))
                    {
                        Console.WriteLine($"User '{username}' has timed out.");

                        // Notify all clients that the user has left due to timeout
                        await _bus.PubSub.PublishAsync(
                            new UserNotification(
                                $"User '{username}' has left the chat (timeout)."
                            )
                        );
                    }
                }
            }
        }, token);
    }
}
