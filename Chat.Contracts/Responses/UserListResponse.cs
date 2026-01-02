namespace Chat.Contracts;

/// <summary>
/// Represents the response to a <see cref="UserListRequest"/>, containing a list of currently logged in users.
/// </summary>
/// <param name="IsSuccess">Indicates whether the request was successful.</param>
/// <param name="UserList">A list containing the usernames of all currently logged in users.</param>
public record UserListResponse(bool IsSuccess, IReadOnlyList<string> UserList);