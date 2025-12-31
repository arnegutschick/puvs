namespace Chat.Contracts;

public record SubmitMessageCommand(string Username, string Text);

public record StatisticsRequest(string RequestingUser);

public record TopChatter(string User, long Messages);

public record StatisticsResponse(
    long TotalMessages,
    double AvgMessagesPerUser,
    IReadOnlyList<TopChatter> Top3
);