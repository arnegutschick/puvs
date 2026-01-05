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

    private Timer? _serverHeartbeatTimer;
    private IConfiguration _configuration;

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
        _configuration = configuration;

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
    /// Starts periodically sending server heartbeat events to all clients.
    /// This allows clients to detect whether the server is online or offline.
    /// The interval is configurable via "ChatSettings:ServerHeartbeatIntervalSeconds" in appsettings.json.
    /// </summary>
    public void StartServerHeartbeat()
    {
        int intervalSeconds = _configuration.GetValue("ChatSettings:ServerHeartbeatIntervalSeconds", 30);
        _serverHeartbeatTimer = new Timer(_ =>
        {
            try
            {
                _bus.PubSub.Publish(new ServerHeartbeat(DateTime.UtcNow));
            }
            catch (Exception)
            {
                Console.WriteLine($"[ERROR] Failed to send ServerHeartbeat.");
            }
        }, null, TimeSpan.Zero, TimeSpan.FromSeconds(intervalSeconds));
    }


    /// <summary>
    /// Stops sending server heartbeat events to clients by disposing the underlying timer.
    /// Should be called when the server is shutting down to prevent lingering timers.
    /// </summary>
    public void StopServerHeartbeat()
    {
        _serverHeartbeatTimer?.Dispose();
    }


    /// <summary>
    /// Starts a background task that periodically checks for timed-out users.
    /// Removes timed-out users and publishes <see cref="UserNotification"/> events.
    /// </summary>
    /// <param name="token">Optional cancellation token to stop the cleanup loop.</param>
    /// <returns>A Task representing the background cleanup loop.</returns>
    public async Task StartCleanupTask(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_timeout, token);

                var timedOut = _users.GetTimedOut(_timeout);

                foreach (var username in timedOut)
                {
                    try
                    {
                        if (_users.Remove(username))
                        {
                            Console.WriteLine($"User '{username}' has timed out.");

                            await _bus.PubSub.PublishAsync(
                                new UserNotification(
                                    $"User '{username}' has left the chat due to timeout."
                                )
                            );
                        }
                    }
                    catch (Exception)
                    {
                        Console.WriteLine($"[ERROR] Failed to remove or notify user '{username}'.");
                    }
                }
            }
            catch (TaskCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Heartbeat cleanup task failed: {ex.Message}");
                // Add small delay before retry
                await Task.Delay(TimeSpan.FromSeconds(1), token);
            }
        }
    }
}