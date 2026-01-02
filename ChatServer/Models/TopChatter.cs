namespace ChatServer;

/// <summary>
/// Represents a user and their total number of messages,
/// used for generating leaderboards or top chatters statistics.
/// </summary>
/// <param name="UserName">The username of the chat participant.</param>
/// <param name="MessageCount">The total number of messages sent by the user.</param>
public record TopChatter(string UserName, int MessageCount);