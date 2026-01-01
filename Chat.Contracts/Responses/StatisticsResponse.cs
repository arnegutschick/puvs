namespace Chat.Contracts;


public record TopChatterDto(string User, int MessageCount);

public record StatisticsResponse(
    long TotalMessages,
    double AvgMessagesPerUser,
    IReadOnlyList<TopChatterDto> Top3
);