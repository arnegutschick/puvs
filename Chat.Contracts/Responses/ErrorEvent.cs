namespace Chat.Contracts;

/// <summary>
/// Represents an event that conveys an error message to subscribers.
/// </summary>
/// <param name="Message">The error message describing what went wrong.</param>
public record ErrorEvent(string Message);