namespace Chat.Contracts;

/// <summary>
/// Represents a command to send a file from a user to one or more recipients.
/// </summary>
/// <param name="SenderUsername">The username of the user sending the file.</param>
/// <param name="FileName">The name of the file being sent, including extension.</param>
/// <param name="ContentBase64">The file content encoded as a Base64 string.</param>
/// <param name="FileSizeBytes">The size of the file in bytes.</param>
public record SendFileCommand(
    string SenderUsername,
    string FileName,
    string ContentBase64,
    long FileSizeBytes
);
