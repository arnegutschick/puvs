namespace Chat.Contracts;

/// <summary>
/// Represents a heartbeat message sent from a client to the server
/// to indicate that the client is still online.
/// </summary>
/// <param name="Username">The username of the client sending the heartbeat.</param>
public record ClientHeatbeat(string Username);

/// <summary>
/// Represents a heartbeat message sent from the server to all clients
/// to indicate that the server is online.
/// </summary>
/// <param name="Timestamp">The UTC timestamp when the heartbeat was sent.</param>
public record ServerHeartbeat(DateTime Timestamp);