namespace Chat.Contracts;

/// <summary>
/// Represents a heartbeat message sent to keep the connection alive.
/// </summary>
/// <param name="Username">The username of the client sending the heartbeat.</param>
public record Heartbeat(string Username);