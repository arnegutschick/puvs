using EasyNetQ;
using Chat.Contracts;

namespace ChatClient;

internal static class Program
{
    private static IBus bus = null!;
    private static string username = "";
    private static string userColor = "White";

    static void Main()
    {
        try
        {
            // Create RabbitMQ bus
            bus = RabbitHutch.CreateBus("host=localhost");
            Console.WriteLine("Connected to RabbitMQ.");

            // Ask user for username
            Console.Write("Enter username: ");
            username = Console.ReadLine() ?? Guid.NewGuid().ToString();

            // Perform login via RPC
            var loginResponse = bus.Rpc.Request<LoginRequest, LoginResponse>(
                new LoginRequest(username)
            );

            if (!loginResponse.IsSuccess)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Login failed: {loginResponse.Reason}");
                Console.ResetColor();
                Console.WriteLine("Press [Enter] to exit.");
                Console.ReadLine();
                return;
            }

            // Store assigned color
            userColor = loginResponse.UserColor;

            Console.Clear();
            Console.WriteLine($"Welcome, {username}! You are now in the chat. Type '/quit' to exit.");
            Console.ForegroundColor = GetConsoleColor(userColor);
            Console.WriteLine($"Your assigned color is: {userColor}");
            Console.ResetColor();
            Console.WriteLine("-----------------------------------------------------------------");

            string subscriptionId = $"chat_client_{Guid.NewGuid()}";
            string privateTopic = $"private_{username.ToLowerInvariant()}";

            // --- Subscribe to Broadcasts (Chat Messages) ---
            bus.PubSub.Subscribe<BroadcastMessageEvent>(subscriptionId, msg =>
            {
                ClearCurrentConsoleLine();
                
                // Set color based on message
                if (Enum.TryParse<ConsoleColor>(msg.UserColor, out var messageColor))
                {
                    Console.ForegroundColor = messageColor;
                }
                
                Console.WriteLine($"{msg.Username}: {msg.Text}");
                Console.ResetColor();
                Console.Write("Your message: "); // Show prompt again
            });

            // --- Subscribe to User Notifications ---
            bus.PubSub.Subscribe<UserNotification>(subscriptionId, notification =>
            {
                ClearCurrentConsoleLine();
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(notification.Text);
                Console.ResetColor();
                Console.Write("Your message: "); // Show prompt again
            });

            // --- Subscribe to Private Messages ---
            bus.PubSub.Subscribe<PrivateMessageEvent>(
                privateTopic,
                privateMessage =>
                {
                    ClearCurrentConsoleLine();
                    
                    if (Enum.TryParse<ConsoleColor>(privateMessage.UserColor, out var pmColor))
                    {
                        Console.ForegroundColor = pmColor;
                    }
                    
                    if (privateMessage.IsOutgoing)
                    {
                        Console.WriteLine($"[PRIVATE → {privateMessage.RecipientUsername}] {privateMessage.Text}");
                    }
                    else
                    {
                        Console.WriteLine($"[PRIVATE ← {privateMessage.SenderUsername}] {privateMessage.Text}");
                    }
                    Console.ResetColor();
                    Console.Write("Your message: "); // Show prompt again
                },
                cfg => cfg.WithTopic(privateTopic)
            );

            // --- Subscribe to File Events ---
            bus.PubSub.Subscribe<FileReceivedEvent>(subscriptionId, file =>
            {
                if (file.Sender == username) return;

                ClearCurrentConsoleLine();
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"[FILE] {file.Sender}: {file.FileName} ({file.FileSizeBytes} bytes)");
                Console.WriteLine($"      Download? Save to: ~/Downloads/Chat/{file.FileName}");
                Console.ResetColor();
                Console.Write("Your message: "); // Show prompt again
            });

            // Start heartbeat
            var heartbeatTimer = new System.Threading.Timer(_ =>
            {
                bus.PubSub.Publish(new Heartbeat(username));
            }, null, TimeSpan.Zero, TimeSpan.FromSeconds(10));

            // --- Main Loop to Send Messages ---
            Console.Write("Your message: "); // Initial prompt
            while (true)
            {
                string input = Console.ReadLine() ?? string.Empty;

                if (string.IsNullOrWhiteSpace(input)) 
                {
                    Console.Write("Your message: ");
                    continue;
                }

                if (input.Equals("/quit", StringComparison.OrdinalIgnoreCase))
                {
                    heartbeatTimer.Dispose();
                    bus.PubSub.Publish(new LogoutRequest(username));
                    break;
                }

                // Private message
                if (input.StartsWith("/msg ", StringComparison.OrdinalIgnoreCase))
                {
                    string payload = input.Substring(5).Trim();
                    string[] parts = payload.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);

                    if (parts.Length < 2)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("[ERROR] Usage: /msg <username> <message>");
                        Console.ResetColor();
                        Console.Write("Your message: ");
                        continue;
                    }

                    var command = new SendPrivateMessageCommand(username, parts[0], parts[1]);
                    bus.PubSub.Publish(command);
                    Console.Write("Your message: ");
                    continue;
                }

                // Public message
                var submitCommand = new SubmitMessageCommand(username, input);
                bus.PubSub.Publish(submitCommand);
                Console.Write("Your message: ");
            }

            // Cleanup
            bus.Dispose();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"An error occurred: {ex.Message}");
            Console.WriteLine("Please ensure RabbitMQ and the ChatServer are running.");
            Console.ResetColor();
        }

        Console.WriteLine("You have left the chat. Press [Enter] to exit.");
        Console.ReadLine();
    }

    private static void ClearCurrentConsoleLine()
    {
        int currentLineCursor = Console.CursorTop;
        Console.SetCursorPosition(0, Console.CursorTop);
        Console.Write(new string(' ', Console.WindowWidth));
        Console.SetCursorPosition(0, currentLineCursor);
    }

    private static ConsoleColor GetConsoleColor(string colorName)
    {
        return colorName switch
        {
            "Blue" => ConsoleColor.Blue,
            "Green" => ConsoleColor.Green,
            "Magenta" => ConsoleColor.Magenta,
            "Cyan" => ConsoleColor.Cyan,
            "Red" => ConsoleColor.Red,
            "Yellow" => ConsoleColor.Yellow,
            "DarkBlue" => ConsoleColor.DarkBlue,
            "DarkGreen" => ConsoleColor.DarkGreen,
            "DarkMagenta" => ConsoleColor.DarkMagenta,
            "DarkCyan" => ConsoleColor.DarkCyan,
            "DarkRed" => ConsoleColor.DarkRed,
            "DarkYellow" => ConsoleColor.DarkYellow,
            _ => ConsoleColor.White
        };
    }
}
