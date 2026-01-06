using Chat.Contracts;
using EasyNetQ;
using ChatClient.Infrastructure;

namespace ChatClient.Services
{
    /// <summary>
    /// Handles sending private messages to specific users.
    /// Validates that the recipient differs from the sender and uses the bus publisher for error handling.
    /// </summary>
    public class PrivateMessageService
    {
        private readonly IBus _bus;
        private readonly BusPublisher _busPublisher;
        private readonly Action<string, string> _appendMessage;
        private readonly string _username;

        /// <summary>
        /// Initializes a new instance of the <see cref="PrivateMessageService"/> class.
        /// </summary>
        /// <param name="bus">The Pub/Sub bus for sending private message commands.</param>
        /// <param name="busPublisher">Helper for error-safe publish operations.</param>
        /// <param name="appendMessage">Callback to append error messages to the UI.</param>
        /// <param name="username">The current user's username.</param>
        public PrivateMessageService(IBus bus, BusPublisher busPublisher, Action<string, string> appendMessage, string username)
        {
            _bus = bus;
            _busPublisher = busPublisher;
            _appendMessage = appendMessage;
            _username = username;
        }

        /// <summary>
        /// Sends a private message to a specific user.
        /// Validates that the recipient is not the current user.
        /// </summary>
        /// <param name="recipientUsername">The username of the message recipient.</param>
        /// <param name="text">The message content to send.</param>
        /// <returns>A task representing the asynchronous send operation.</returns>
        public async Task SendPrivateMessageAsync(string recipientUsername, string text)
        {
            if (string.Equals(recipientUsername, _username, StringComparison.OrdinalIgnoreCase))
            {
                _appendMessage("[ERROR] You cannot send a private message to yourself.", "Red");
                return;
            }

            await _busPublisher.TryPublishAsync(() => _bus.PubSub.PublishAsync(new SendPrivateMessageCommand(_username, recipientUsername, text)));
        }
    }
}
