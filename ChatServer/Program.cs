using EasyNetQ;
using Chat.Contracts;
using System.Collections.Concurrent;

namespace ChatServer;

internal class Program
{
    public record UserInfo(DateTime LastHeartbeat, string Color);

    /// <summary>
    /// Thread-safe dictionary to track connected users.
    /// Key: username, Value: User information that contains:
    /// - A timestamp that gets updated every 10 seconds to ensure the user is still active and
    /// - The color in which user messages will appear in the chat.
    /// </summary>
    private static readonly ConcurrentDictionary<string, UserInfo> ConnectedUsers = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Predefined color pool for users.
    /// </summary>
    private static readonly string[] ColorPool = new[]
    {
        "Blue", "Green", "Magenta", "Cyan", "Brown",
        "BrightBlue", "BrightMagenta", "BrightCyan", "BrightRed"
    };

    private static int _colorIndex = 0;

    private static readonly StatisticsStore Stats = new();

    private static async Task Main(string[] args)
    {
        Console.WriteLine("ChatServer is starting...");

        try
        {
            // Create a connection to RabbitMQ
            using IBus bus = RabbitHutch.CreateBus("host=localhost");
            Console.WriteLine("Connected to RabbitMQ.");

            // --- RPC: Handle Login Requests ---
            await bus.Rpc.RespondAsync<LoginRequest, LoginResponse>(async request =>
            {
                string username = request.Username?.Trim() ?? string.Empty;
                Console.WriteLine($"Login request for user: '{username}'");

                if (string.IsNullOrWhiteSpace(username))
                    return DenyLogin(username, "Username cannot be empty.");

                if (username.Contains(' '))
                    return DenyLogin(username, "Username must be a single word (no spaces).");

                if (!username.All(char.IsLetterOrDigit))
                    return DenyLogin(username, "Username may only contain letters and numbers.");

                // Assign color cyclically from the pool
                string assignedColor;
                if (_colorIndex >= int.MaxValue - 1) _colorIndex = 0;
                int index = Interlocked.Increment(ref _colorIndex);
                assignedColor = ColorPool[index % ColorPool.Length];

                // Try to register user atomar
                if (!ConnectedUsers.TryAdd(username, new UserInfo(DateTime.UtcNow, assignedColor)))
                    return DenyLogin(username, "User is already logged in.");

                Console.WriteLine($"User '{username}' logged in successfully with color '{assignedColor}'.");

                Stats.RegisterUser(username);

                await bus.PubSub.PublishAsync(
                    new UserNotification($"*** User '{username}' has joined the chat. ***")
                );

                return new LoginResponse(true, string.Empty);
            });

            // --- RPC: Handle Statistics Requests ---
            await bus.Rpc.RespondAsync<StatisticsRequest, StatisticsResponse>(request =>
            {
                var (total, avg, top3) = Stats.BuildSnapshot();

                return Task.FromResult(new StatisticsResponse(
                    TotalMessages: total,
                    AvgMessagesPerUser: avg,
                    Top3: top3.Select(t => new TopChatter(t.User, t.Count)).ToList()
                ));
            });

            // --- Pub/Sub: Handle incoming commands from clients to submit a message ---
            await bus.PubSub.SubscribeAsync<SubmitMessageCommand>("chat_server_submit_message_subscription", async command =>
            {
                Console.WriteLine($"Received message from '{command.Username}': '{command.Text}'");

                // Get the user's assigned color
                string userColor = ConnectedUsers.TryGetValue(command.Username, out var UserInfo) ? UserInfo.Color : "White";

                var text = command.Text?.Trim() ?? "";

                if (!text.StartsWith("/")) Stats.RecordMessage(command.Username);

                // Create the event that will be broadcast to all clients
                BroadcastMessageEvent broadcastEvent = new BroadcastMessageEvent(command.Username, text, userColor);

                // Broadcast the event to all clients
                await bus.PubSub.PublishAsync(broadcastEvent);
                Console.WriteLine($"Broadcasted message to all clients.");
            });

            // --- Pub/Sub: Handle Logout Requests ---
            await bus.PubSub.SubscribeAsync<LogoutRequest>("chat_server_logout_subscription", async request =>
            {
                Console.WriteLine($"Logout request for user: '{request.Username}'");

                // Remove user from tracking
                ConnectedUsers.TryRemove(request.Username, out _);

                Console.WriteLine($"User '{request.Username}' logged out.");
                // Announce the departure to all clients
                await bus.PubSub.PublishAsync(new UserNotification($"*** User '{request.Username}' has left the chat. ***"));
            });

            // --- Pub/Sub: Handle Private Messages ---
            await bus.PubSub.SubscribeAsync<SendPrivateMessageCommand>("chat_server_private_message_subscription", async command =>
            {
                Console.WriteLine($"Private message from '{command.SenderUsername}' to '{command.RecipientUsername}': '{command.Text}'");

                // Validate the recipient exists
                string recipientKey = command.RecipientUsername.Trim();
                string senderTopic = $"private_{command.SenderUsername.ToLowerInvariant()}";

                if (!ConnectedUsers.TryGetValue(recipientKey, out var UserInfo))
                {
                    var errorEvent = new PrivateMessageEvent(
                        "System",
                        command.SenderUsername,
                        $"User '{command.RecipientUsername}' is not online or does not exist.",
                        "Red",
                        false
                    );

                    await bus.PubSub.PublishAsync(errorEvent, senderTopic);
                    Console.WriteLine($"Private message from '{command.SenderUsername}' couldn't be delivered; Recipient does not exist.");
                    return;
                }

                // Send private message to recipient
                PrivateMessageEvent recipientEvent = new PrivateMessageEvent(
                    command.SenderUsername,
                    command.RecipientUsername,
                    command.Text,
                    UserInfo.Color,
                    false
                );

                string recipientTopic = $"private_{command.RecipientUsername.ToLowerInvariant()}";
                await bus.PubSub.PublishAsync(recipientEvent, recipientTopic);
                Console.WriteLine($"Private message delivered to '{command.RecipientUsername}'.");

                // Send confirmation copy to sender
                PrivateMessageEvent senderEvent = new PrivateMessageEvent(
                    command.SenderUsername,
                    command.RecipientUsername,
                    command.Text,
                    UserInfo.Color,
                    true // Mark as outgoing for sender display
                );
                await bus.PubSub.PublishAsync(senderEvent, senderTopic);
            });

            await bus.PubSub.SubscribeAsync<SendFileCommand>("chat_server_file_subscription", async command =>
            {
                Console.WriteLine(
                    $"File received from '{command.Sender}': {command.FileName} ({command.FileSizeBytes} bytes)"
                );

                // Broadcast to all clients
                var fileEvent = new FileReceivedEvent(command.Sender, command.FileName, command.ContentBase64, command.FileSizeBytes);
                await bus.PubSub.PublishAsync(fileEvent);
            });

            await bus.PubSub.SubscribeAsync<Heartbeat>("chat_server_heartbeat", hb =>
            {
                ConnectedUsers.AddOrUpdate(
                    hb.Username,
                    _ => new UserInfo(DateTime.UtcNow, "White"),
                    (_, old) => old with { LastHeartbeat = DateTime.UtcNow }
                );

                return Task.CompletedTask;
            });

            StartCleanupTask(bus);

            Console.WriteLine("Server is running. Press [Enter] to exit.");
            Console.ReadLine();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"An error occurred: {ex.Message}");
            Console.WriteLine("Please ensure RabbitMQ is running and accessible at 'localhost'.");
            Console.ResetColor();
        }

        Console.WriteLine("ChatServer is shutting down.");
    }

    /// <summary>
    /// Starts a background task that periodically checks for users who have timed out.
    /// Users who have not sent a heartbeat within the timeout period (30 seconds) 
    /// are removed from the ConnectedUsers dictionary, and a 
    /// UserNotification is published to inform other clients that 
    /// the user has left the chat due to timeout.
    /// </summary>
    /// <param name="bus">The IBus instance used to publish notifications.</param>
    private static void StartCleanupTask(IBus bus)
    {
        _ = Task.Run(async () =>
        {
            while (true)
            {
                await Task.Delay(TimeSpan.FromSeconds(30));

                DateTime now = DateTime.UtcNow;

                foreach (var user in ConnectedUsers)
                {
                    if (now - user.Value.LastHeartbeat > TimeSpan.FromSeconds(30))
                    {
                        if (ConnectedUsers.TryRemove(user.Key, out var removedUser))
                        {
                            Console.WriteLine($"User '{user.Key}' timed out.");

                            await bus.PubSub.PublishAsync(
                                new UserNotification(
                                    $"*** User '{user.Key}' has left the chat (timeout). ***"
                                )
                            );
                        }
                    }
                }
            }
        });
    }

    /// <summary>
    /// Helper method to generate a failed LoginResponse and log the reason to the console.
    /// </summary>
    /// <param name="username">The username that attempted to log in.</param>
    /// <param name="reason">The reason why the login was denied.</param>
    /// <returns>A LoginResponse instance indicating failure, containing the provided reason.</returns>
    private static LoginResponse DenyLogin(string username, string reason)
    {
        Console.WriteLine($"Login request for user '{username}' denied; {reason}");
        return new LoginResponse(false, reason);
    }
}