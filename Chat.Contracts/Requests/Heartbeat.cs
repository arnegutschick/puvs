namespace Chat.Contracts;

/// <summary>
/// Represents a heartbeat message sent to keep the connection alive.
/// </summary>
public record Heartbeat(string Username);