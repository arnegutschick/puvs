namespace Chat.Contracts;

/// <summary>
/// Represents the response to a login request.
/// </summary>
/// <param name="IsSuccess">Indicates whether the login was successful.</param>
/// <param name="Reason">The reason for a failed login, or an optional message for successful login.</param>
public record LoginResponse(bool IsSuccess, string Reason);
