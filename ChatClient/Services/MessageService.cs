using Chat.Contracts;
using EasyNetQ;
using Terminal.Gui;
using ChatClient.Infrastructure;

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
        private readonly Func<bool> _isServerReachable;

        /// <summary>
        /// Initializes a new instance of the <see cref="MessageService"/> class.
        /// </summary>
        /// <param name="bus">The Pub/Sub bus for sending messages and RPC requests.</param>
        /// <param name="busPublisher">Helper for error-safe publish operations.</param>
        /// <param name="appendMessage">Callback to append responses to the UI.</param>
        /// <param name="username">The current user's username.</param>
        /// /// <param name="isServerReachable">
        /// Delegate that returns <c>true</c> if the chat server and message bus
        /// are currently considered reachable, or <c>false</c> otherwise.
        /// This is used as a precondition check before attempting publish operations
        /// to avoid unnecessary failures when the server or RabbitMQ are offline.
        /// </param>
        public MessageService(IBus bus, BusPublisher busPublisher, Action<string, string> appendMessage, string username, Func<bool> isServerReachable)
        {
            _bus = bus;
            _busPublisher = busPublisher;
            _appendMessage = appendMessage;
            _username = username;
            _isServerReachable = isServerReachable;
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
            if (!_isServerReachable())
            {
                Application.MainLoop.Invoke(() =>
                {
                    _appendMessage("[ERROR] Either the server or RabbitMQ are offline.", "Red");
                });
                return;
            }
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var requestTask = _bus.Rpc.RequestAsync<TimeRequest, TimeResponse>(new TimeRequest(_username));
                var res = await requestTask.WaitAsync(cts.Token);

                if (res.IsSuccess)
                    Application.MainLoop.Invoke(() => _appendMessage($"[INFO] Current server time: {res.CurrentTime}", "Black"));
            }
            catch (OperationCanceledException)
            {
                Application.MainLoop.Invoke(() => _appendMessage("[ERROR] Request timed out. Is RabbitMQ running?", "Red"));
            }
            catch (Exception)
            {
                Application.MainLoop.Invoke(() => _appendMessage($"[ERROR] Request failed.", "Red"));
            }
        }

        /// <summary>
        /// Requests the list of currently logged-in users via RPC and displays it in the UI.
        /// </summary>
        /// <returns>A task representing the asynchronous RPC request.</returns>
        public async Task HandleUsersCommandAsync()
        {
            if (!_isServerReachable())
            {
                Application.MainLoop.Invoke(() =>
                {
                    _appendMessage("[ERROR] Either the server or RabbitMQ are offline.", "Red");
                });
                return;
            }

            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var requestTask = _bus.Rpc.RequestAsync<UserListRequest, UserListResponse>(
                    new UserListRequest(_username)
                );
                var res = await requestTask.WaitAsync(cts.Token);

                if (res.IsSuccess)
                {
                    Application.MainLoop.Invoke(() =>
                    {
                        _appendMessage("=== Currently logged in users ===", "Black");
                        foreach (var user in res.UserList)
                        {
                            _appendMessage($"- {user}", "Black");
                        }
                        _appendMessage("==================================", "Black");
                    });
                }
            }
            catch (OperationCanceledException)
            {
                Application.MainLoop.Invoke(() => _appendMessage("[ERROR] Request timed out. Is RabbitMQ running?", "Red"));
            }
            catch (Exception)
            {
                Application.MainLoop.Invoke(() => _appendMessage("[ERROR] Failed to fetch users.", "Red"));
            }
        }

        /// <summary>
        /// Requests chat statistics (total messages, average per user, top 3 chatters) via RPC and displays them in the UI.
        /// </summary>
        /// <returns>A task representing the asynchronous RPC request.</returns>
        public async Task HandleStatisticsCommandAsync()
        {
            if (!_isServerReachable())
            {
                Application.MainLoop.Invoke(() =>
                {
                    _appendMessage("[ERROR] Either the server or RabbitMQ are offline.", "Red");
                });
                return;
            }

            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var requestTask = _bus.Rpc.RequestAsync<StatisticsRequest, StatisticsResponse>(
                    new StatisticsRequest(_username)
                );
                var res = await requestTask.WaitAsync(cts.Token);

                if (res.IsSuccess)
                {
                    Application.MainLoop.Invoke(() =>
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
                    });
                }
            }
            catch (OperationCanceledException)
            {
                Application.MainLoop.Invoke(() => _appendMessage("[ERROR] Request timed out. Is RabbitMQ running?", "Red"));
            }
            catch (Exception)
            {
                Application.MainLoop.Invoke(() => _appendMessage("[ERROR] Failed to fetch statistics.", "Red"));
            }
        }
    }
}
