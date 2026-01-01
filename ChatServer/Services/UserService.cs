using Chat.Contracts;
using EasyNetQ;
using System.Collections.Concurrent;

namespace ChatServer.Services;

public class UserService
{
    private readonly ConcurrentDictionary<string, UserInfo> _users;
    private readonly string[] _colorPool;
    private int _colorIndex = 0;

    public UserService(ConcurrentDictionary<string, UserInfo> users, string[] colorPool)
    {
        _users = users;
        _colorPool = colorPool;
    }

    /// <summary>
    /// Handle a login attempt.
    /// Returns a LoginResponse indicating success/failure.
    /// Publishes a UserNotification on successful login.
    /// </summary>
    public async Task<LoginResponse> HandleLoginAsync(LoginRequest request, IBus bus)
    {
        string username = request.Username?.Trim() ?? string.Empty;
        Console.WriteLine($"Login request for user: '{username}'");

        if (string.IsNullOrWhiteSpace(username))
            return Deny(username, "Username cannot be empty.");

        if (username.Contains(' '))
            return Deny(username, "Username must be a single word (no spaces).");

        if (!username.All(char.IsLetterOrDigit))
            return Deny(username, "Username may only contain letters and numbers.");

        // Assign color cyclically
        if (_colorIndex >= int.MaxValue - 1) _colorIndex = 0;
        int index = Interlocked.Increment(ref _colorIndex);
        string assignedColor = _colorPool[index % _colorPool.Length];

        if (!_users.TryAdd(username, new UserInfo(DateTime.UtcNow, assignedColor)))
            return Deny(username, "User is already logged in.");

        Console.WriteLine($"User '{username}' logged in successfully with color '{assignedColor}'.");

        await bus.PubSub.PublishAsync(new UserNotification($"*** User '{username}' has joined the chat. ***"));

        return new LoginResponse(true, string.Empty);
    }

    /// <summary>
    /// Handle a logout request.
    /// Removes the user and publishes a UserNotification.
    /// </summary>
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
    /// Update heartbeat timestamp for a user.
    /// If user does not exist, optionally adds them with default color "White".
    /// </summary>
    public void UpdateHeartbeat(string username)
    {
        _users.AddOrUpdate(
            username,
            _ => new UserInfo(DateTime.UtcNow, "Black"),
            (_, old) => old with { LastHeartbeat = DateTime.UtcNow }
        );
    }

    /// <summary>
    /// Returns the list of usernames that have timed out.
    /// </summary>
    public IEnumerable<string> GetTimedOut(TimeSpan timeout)
    {
        var now = DateTime.UtcNow;
        return _users.Where(kv => now - kv.Value.LastHeartbeat > timeout)
                     .Select(kv => kv.Key)
                     .ToList();
    }

    /// <summary>
    /// Removes a user by username.
    /// Returns true if removed.
    /// </summary>
    public bool Remove(string username)
    {
        return _users.TryRemove(username, out _);
    }

    /// <summary>
    /// Get user color, defaults to White if not found.
    /// </summary>
    public string GetColor(string username)
    {
        return _users.TryGetValue(username, out var info) ? info.Color : "Black";
    }

    private static LoginResponse Deny(string username, string reason)
    {
        Console.WriteLine($"Login request for user '{username}' denied; {reason}");
        return new LoginResponse(false, reason);
    }
}
