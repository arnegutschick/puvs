namespace Chat.Contracts;

/// <summary>
/// Command sent from a client to the server to deliver a private message to a specific recipient.
/// </summary>
/// <param name="SenderUsername">The username of the message sender.</param>
/// <param name="RecipientUsername">The username of the intended message recipient.</param>
/// <param name="Text">The content of the private message.</param>
public record SendPrivateMessageCommand(string SenderUsername, string RecipientUsername, string Text);
