namespace ChatServer;

/// <summary>
/// Represents information about a connected user.
/// </summary>
/// <param name="LastHeartbeat">The UTC timestamp of the user's last heartbeat, used to detect timeouts.</param>
/// <param name="Color">The assigned chat color for the user.</param>
public record UserInfo(DateTime LastHeartbeat, string Color);