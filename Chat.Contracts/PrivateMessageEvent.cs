namespace Chat.Contracts;

/// <summary>
/// Event published by the server to deliver a private message to a specific client.
/// Uses topic-based routing where the topic is the recipient's username.
/// </summary>
/// <param name="SenderUsername">The username of the message sender.</param>
/// <param name="RecipientUsername">The username of the message recipient.</param>
/// <param name="Text">The content of the private message.</param>
/// <param name="IsOutgoing">True if this is a copy sent back to the sender for confirmation.</param>
public record PrivateMessageEvent(string SenderUsername, string RecipientUsername, string Text, bool IsOutgoing = false);
