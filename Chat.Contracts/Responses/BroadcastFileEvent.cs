namespace Chat.Contracts;

/// <summary>
/// Represents an event that broadcasts a file to all subscribers.
/// </summary>
/// <param name="Sender">The username of the user sending the file.</param>
/// <param name="FileName">The name of the file being sent, including its extension.</param>
/// <param name="ContentBase64">The file content encoded as a Base64 string.</param>
/// <param name="FileSizeBytes">The size of the file in bytes.</param>
public record BroadcastFileEvent(
    string Sender,
    string FileName,
    string ContentBase64,
    long FileSizeBytes
);
