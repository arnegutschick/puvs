namespace Chat.Contracts;

/// <summary>
/// Represents a request to retrieve the current server time.
/// </summary>
/// <param name="SenderUsername">The username of the user sending the request.</param>
public record UserListRequest(string SenderUsername);
