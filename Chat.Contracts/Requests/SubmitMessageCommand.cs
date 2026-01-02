namespace Chat.Contracts;

/// <summary>
/// Represents a command to submit a chat message from a user.
/// </summary>
/// <param name="Username">The username of the user sending the message.</param>
/// <param name="Text">The text content of the message.</param>
public record SubmitMessageCommand(string Username, string Text);
