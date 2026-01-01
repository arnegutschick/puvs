using Chat.Contracts;
using EasyNetQ;
using System.Collections.Concurrent;

namespace ChatServer.Services;

/// <summary>
/// Service responsible for managing connected users, handling login/logout,
/// assigning colors, and tracking heartbeat timestamps.
/// Publishes notifications to all users via the message bus.
/// </summary>
public class UserService
{
    private readonly ConcurrentDictionary<string, UserInfo> _users;
    private readonly string[] _colorPool;
    private int _colorIndex = 0;

    /// <summary>
    /// Initializes the UserService with a shared user collection and color pool.
    /// </summary>
    /// <param name="users">Thread-safe collection of active users.</param>
    /// <param name="colorPool">Array of colors to assign to users.</param>
    public UserService(ConcurrentDictionary<string, UserInfo> users, string[] colorPool)
    {
        _users = users;
        _colorPool = colorPool;
    }


    /// <summary>
    /// Handles a login request.
    /// Validates the username, assigns a color, and adds the user to the active collection.
    /// Publishes a <see cref="UserNotification"/> to inform all clients about the new user.
    /// </summary>
    /// <param name="request">Login request containing the username.</param>
    /// <param name="bus">Message bus for publishing notifications.</param>
    /// <returns>A <see cref="LoginResponse"/> indicating success or failure.</returns>
    public async Task<LoginResponse> HandleLoginAsync(LoginRequest request, IBus bus)
    {
        string username = request.Username?.Trim() ?? string.Empty;
        Console.WriteLine($"Login request for user: '{username}'");

        // --- Validate username ---
        if (string.IsNullOrWhiteSpace(username))
            return Deny(username, "Username cannot be empty.");
        if (username.Contains(' '))
            return Deny(username, "Username must be a single word (no spaces).");
        if (!username.All(char.IsLetterOrDigit))
            return Deny(username, "Username may only contain letters and numbers.");

        // --- Assign color cyclically ---
        if (_colorIndex >= int.MaxValue - 1) _colorIndex = 0; // Prevent value overflow
        int index = Interlocked.Increment(ref _colorIndex);
        string assignedColor = _colorPool[index % _colorPool.Length];

        // --- Attempt to add user ---
        if (!_users.TryAdd(username, new UserInfo(DateTime.UtcNow, assignedColor)))
            return Deny(username, "User is already logged in.");

        Console.WriteLine($"User '{username}' logged in successfully with color '{assignedColor}'.");

        // --- Notify other users ---
        await bus.PubSub.PublishAsync(new UserNotification($"*** User '{username}' has joined the chat. ***"));

        return new LoginResponse(true, string.Empty);
    }


    /// <summary>
    /// Handles a logout request.
    /// Removes the user from the active collection and publishes a <see cref="UserNotification"/>.
    /// </summary>
    /// <param name="request">Logout request containing the username.</param>
    /// <param name="bus">Message bus for publishing notifications.</param>
    public async Task HandleLogoutAsync(LogoutRequest request, IBus bus)
    {
        if (_users.TryRemove(request.Username, out _))
        {
            Console.WriteLine($"User '{request.Username}' logged out.");
            await bus.PubSub.PublishAsync(new UserNotification(
                $"*** User '{request.Username}' has left the chat. ***"
            ));
        }
    }


    /// <summary>
    /// Updates the last heartbeat timestamp for a user.
    /// If the user does not exist, optionally adds them with default color "Black".
    /// </summary>
    /// <param name="username">The username to update.</param>
    public void UpdateHeartbeat(string username)
    {
        _users.AddOrUpdate(
            username,
            _ => new UserInfo(DateTime.UtcNow, "Black"),        // Add new user if missing
            (_, old) => old with { LastHeartbeat = DateTime.UtcNow } // Update timestamp if exists
        );
    }


    /// <summary>
    /// Returns a list of usernames that have timed out.
    /// </summary>
    /// <param name="timeout">Time interval to consider a user timed out.</param>
    /// <returns>List of usernames whose last heartbeat exceeds the timeout.</returns>
    public IEnumerable<string> GetTimedOut(TimeSpan timeout)
    {
        var now = DateTime.UtcNow;
        return _users.Where(kv => now - kv.Value.LastHeartbeat > timeout)
                     .Select(kv => kv.Key)
                     .ToList();
    }


    /// <summary>
    /// Removes a user by username.
    /// </summary>
    /// <param name="username">The username to remove.</param>
    /// <returns>True if the user was removed, false otherwise.</returns>
    public bool Remove(string username)
    {
        return _users.TryRemove(username, out _);
    }


    /// <summary>
    /// Helper method to deny a login attempt with a reason.
    /// Logs the denial and returns a failure response.
    /// </summary>
    private static LoginResponse Deny(string username, string reason)
    {
        Console.WriteLine($"Login request for user '{username}' denied; {reason}");
        return new LoginResponse(false, reason);
    }
}