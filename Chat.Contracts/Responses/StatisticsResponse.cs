namespace Chat.Contracts;

/// <summary>
/// Represents a top user in the chat based on the number of messages sent.
/// </summary>
/// <param name="User">The username of the top chatter.</param>
/// <param name="MessageCount">The total number of messages sent by the user.</param>
public record TopChatterDto(string User, int MessageCount);

/// <summary>
/// Represents the response containing chat statistics.
/// </summary>
/// <param name="IsSuccess">Indicates whether the request was successful.</param>
/// <param name="TotalMessages">The total number of messages sent in the chat.</param>
/// <param name="AvgMessagesPerUser">The average number of messages per user.</param>
/// <param name="Top3">A list of the top three chatters, ordered by message count.</param>
public record StatisticsResponse(
    bool IsSuccess,
    long TotalMessages,
    double AvgMessagesPerUser,
    IReadOnlyList<TopChatterDto> Top3
);
