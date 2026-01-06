using Chat.Contracts;
using EasyNetQ;
using ChatClient.Infrastructure;
using Microsoft.Extensions.Configuration;

namespace ChatClient.Services
{
    /// <summary>
    /// Handles public message sending and RPC-based commands (/time, /users, /stats).
    /// Delegates publish and RPC operations through infrastructure helpers.
    /// </summary>
    public class MessageService
    {
        private readonly IBus _bus;
        private readonly BusPublisher _busPublisher;
        private readonly Action<string, string> _appendMessage;
        private readonly string _username;

        /// <summary>
        /// Initializes a new instance of the <see cref="MessageService"/> class.
        /// </summary>
        /// <param name="bus">The Pub/Sub bus for sending messages and RPC requests.</param>
        /// <param name="busPublisher">Helper for error-safe publish operations.</param>
        /// <param name="appendMessage">Callback to append responses to the UI.</param>
        /// <param name="username">The current user's username.</param>
        public MessageService(IBus bus, BusPublisher busPublisher, Action<string, string> appendMessage, string username)
        {
            _bus = bus;
            _busPublisher = busPublisher;
            _appendMessage = appendMessage;
            _username = username;
        }

        /// <summary>
        /// Sends a public message to all connected users.
        /// Ignores empty or whitespace-only messages.
        /// </summary>
        /// <param name="text">The message text to broadcast.</param>
        /// <returns>A task representing the asynchronous send operation.</returns>
        public async Task SendMessageAsync(string text)
        {
            text ??= "";
            var trimmed = text.Trim();

            if (string.IsNullOrWhiteSpace(trimmed))
                return;

            await _busPublisher.TryPublishAsync(() => _bus.PubSub.PublishAsync(new SubmitMessageCommand(_username, trimmed)));
        }

        /// <summary>
        /// Requests the current server time via RPC and displays it in the UI.
        /// </summary>
        /// <returns>A task representing the asynchronous RPC request.</returns>
        public async Task HandleTimeCommandAsync()
        {
            var res = await _bus.Rpc.RequestAsync<TimeRequest, TimeResponse>(new TimeRequest(_username));
            if (res.IsSuccess)
            {
                _appendMessage($"[INFO] Current server time: {res.CurrentTime}", "Black");
            }
        }

        /// <summary>
        /// Requests the list of currently logged-in users via RPC and displays it in the UI.
        /// </summary>
        /// <returns>A task representing the asynchronous RPC request.</returns>
        public async Task HandleUsersCommandAsync()
        {
            var res = await _bus.Rpc.RequestAsync<UserListRequest, UserListResponse>(new UserListRequest(_username));
            if (res.IsSuccess)
            {
                _appendMessage("=== Currently logged in users ===", "Black");
                foreach (var user in res.UserList)
                {
                    _appendMessage($"- {user}", "Black");
                }
                _appendMessage("==================================", "Black");
            }
        }

        /// <summary>
        /// Requests chat statistics (total messages, average per user, top 3 chatters) via RPC and displays them in the UI.
        /// </summary>
        /// <returns>A task representing the asynchronous RPC request.</returns>
        public async Task HandleStatisticsCommandAsync()
        {
            var res = await _bus.Rpc.RequestAsync<StatisticsRequest, StatisticsResponse>(new StatisticsRequest(_username));
            if (res.IsSuccess)
            {
                _appendMessage("=== Statistics ===", "Black");
                _appendMessage($"Total Messages: {res.TotalMessages}", "Black");
                _appendMessage($"Ø Messages per User: {res.AvgMessagesPerUser:F2}", "Black");
                _appendMessage("Top 3 most active Chatters:", "Black");
                if (res.Top3 == null || res.Top3.Count == 0)
                {
                    _appendMessage("  (currently no data)", "Black");
                }
                else
                {
                    int rank = 1;
                    foreach (var t in res.Top3)
                    {
                        _appendMessage($"  {rank}. {t.User}: {t.MessageCount}", "Black");
                        rank++;
                    }
                }
                _appendMessage("=================", "Black");
            }
        }
    }
}
