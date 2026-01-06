using Chat.Contracts;
using ChatClient.Infrastructure;
using EasyNetQ;

namespace ChatClient.Services
{
    /// <summary>
    /// Handles sending and receiving files via the chat system.
    /// Validates file paths and sizes, encodes/decodes Base64 content, and manages file I/O.
    /// </summary>
    public class FileService
    {
        private readonly IBus _bus;
        private readonly BusPublisher _busPublisher;
        private readonly Action<string, string> _appendMessage;
        private readonly string _username;

        /// <summary>
        /// Initializes a new instance of the <see cref="FileService"/> class.
        /// </summary>
        /// <param name="bus">The Pub/Sub bus for sending file commands.</param>
        /// <param name="busPublisher">Helper for error-safe publish operations.</param>
        /// <param name="appendMessage">Callback to append status messages to the UI.</param>
        /// <param name="showFileDialog">Callback to show a file save dialog for incoming files.</param>
        /// <param name="username">The current user's username.</param>
        public FileService(IBus bus, BusPublisher busPublisher, Action<string, string> appendMessage, string username)
        {
            _bus = bus;
            _busPublisher = busPublisher;
            _appendMessage = appendMessage;
            _username = username;
        }

        /// <summary>
        /// Sends a file to all connected users via the chat system.
        /// Validates the file path, size (max 1 MB), reads the file, encodes it in Base64, and publishes it.
        /// </summary>
        /// <param name="path">The local file path to send.</param>
        /// <returns>A task representing the asynchronous send operation.</returns>
        public async Task HandleSendFileAsync(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                _appendMessage("[ERROR] Invalid file path.", "Red");
                return;
            }

            if (!File.Exists(path))
            {
                _appendMessage("[ERROR] File does not exist.", "Red");
                return;
            }

            var info = new FileInfo(path);

            if (info.Length > 1_000_000)
            {
                _appendMessage("[ERROR] File is too large (max 1 MB).", "Red");
                return;
            }

            byte[] bytes = await File.ReadAllBytesAsync(path);
            string base64 = Convert.ToBase64String(bytes);

            await _busPublisher.TryPublishAsync(() => _bus.PubSub.PublishAsync(new SendFileCommand(_username, info.Name, base64, info.Length)));

            _appendMessage($"[YOU] Sent file {info.Name}", "BrightGreen");
        }

        /// <summary>
        /// Saves a received file to the user's Downloads/Chat folder.
        /// Decodes the Base64-encoded content and writes it to disk asynchronously.
        /// </summary>
        /// <param name="file">The broadcast file event containing the file name and Base64-encoded content.</param>
        /// <returns>A task representing the asynchronous save operation.</returns>
        public async Task SaveFileAsync(BroadcastFileEvent file)
        {
            try
            {
                byte[] data = Convert.FromBase64String(file.ContentBase64);

                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Downloads",
                    "Chat"
                );

                Directory.CreateDirectory(dir);

                string path = Path.Combine(dir, file.FileName);

                await File.WriteAllBytesAsync(path, data);

                _appendMessage($"[SAVED] {path}", "BrightGreen");
            }
            catch (Exception)
            {
                _appendMessage("[ERROR] Failed to save file.", "Red");
            }
        }
    }
}
