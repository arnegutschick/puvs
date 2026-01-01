namespace Chat.Contracts;

/// <summary>
/// Represents the response to a <see cref="TimeRequest"/>, containing the current server time.
/// </summary>
/// <param name="CurrentTime">The current date and time on the server.</param>
public record TimeResponse(DateTime CurrentTime);