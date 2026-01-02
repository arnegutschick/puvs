namespace Chat.Contracts;

/// <summary>
/// Represents an event that broadcasts a chat message to all subscribers.
/// </summary>
/// <param name="Username">The username of the user sending the message.</param>
/// <param name="Text">The text content of the message.</param>
/// <param name="UserColor">The display color associated with the user (e.g., for UI purposes).</param>
public record BroadcastMessageEvent(string Username, string Text, string UserColor);

