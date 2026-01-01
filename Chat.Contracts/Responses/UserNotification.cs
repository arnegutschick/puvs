namespace Chat.Contracts;

/// <summary>
/// Represents a notification message sent to a user.
/// </summary>
/// <param name="Text">The content of the notification.</param>
public record UserNotification(string Text);
