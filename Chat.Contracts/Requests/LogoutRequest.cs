namespace Chat.Contracts;

/// <summary>
/// Represents a request to log out a user.
/// </summary>
/// <param name="Username">The username of the user who wants to log out.</param>
public record LogoutRequest(string Username);
