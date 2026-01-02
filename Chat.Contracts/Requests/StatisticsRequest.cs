namespace Chat.Contracts;

/// <summary>
/// Represents a request to retrieve statistics, typically for a specific user.
/// </summary>
/// <param name="RequestingUser">The username of the user requesting the statistics.</param>
public record StatisticsRequest(string RequestingUser);
