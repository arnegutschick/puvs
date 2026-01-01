namespace Chat.Contracts;

/// <summary>
/// Represents a request to log in a user.
/// </summary>
/// <param name="Username">The username of the user attempting to log in.</param>
public record LoginRequest(string Username);
