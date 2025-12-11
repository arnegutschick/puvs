using EasyNetQ;
using Chat.Contracts;

namespace ChatClient;

internal class Program
{
    private static void Main(string[] args)
    {
        try
        {
            using IBus bus = RabbitHutch.CreateBus("host=localhost");
            Console.WriteLine("Connected to RabbitMQ.");

            // --- Login ---
            Console.Write("Enter your username: ");
            string username = Console.ReadLine() ?? Guid.NewGuid().ToString();

            LoginRequest loginRequest = new LoginRequest(username);
            LoginResponse loginResponse = bus.Rpc.Request<LoginRequest, LoginResponse>(loginRequest);

            if (!loginResponse.IsSuccess)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Login failed: {loginResponse.Reason}");
                Console.ResetColor();
                Console.WriteLine("Press [Enter] to exit.");
                Console.ReadLine();
                return;
            }

            Console.Clear();
            Console.WriteLine($"Welcome, {username}! You are now in the chat. Type '/quit' to exit.");
            Console.WriteLine("-----------------------------------------------------------------");
            
            string subscriptionId = $"chat_client_{Guid.NewGuid()}";

            // --- Subscribe to Broadcasts (Chat Messages & User Notifications) ---
            bus.PubSub.Subscribe<BroadcastMessageEvent>(subscriptionId, message =>
            {
                ClearCurrentConsoleLine();
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"{message.Username}: {message.Text}");
                Console.ResetColor();
                Console.Write("Your message: ");
            });

            bus.PubSub.Subscribe<UserNotification>(subscriptionId, notification =>
            {
                ClearCurrentConsoleLine();
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(notification.Text);
                Console.ResetColor();
                Console.Write("Your message: ");
            });

            // --- Main Loop to Send Messages ---
            while (true)
            {
                Console.Write("Your message: ");
                string input = Console.ReadLine() ?? string.Empty;

                if (string.IsNullOrWhiteSpace(input)) continue;

                if (input.Equals("/quit", StringComparison.OrdinalIgnoreCase))
                {
                    // Send logout notification
                    bus.PubSub.Publish(new LogoutRequest(username));
                    break; // Exit the loop
                }

                SubmitMessageCommand command = new SubmitMessageCommand(username, input);

                bus.PubSub.Publish(command);
            }
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

    /// <summary>
    /// Clears the current console line.
    /// </summary>
    private static void ClearCurrentConsoleLine()
    {
        int currentLineCursor = Console.CursorTop;
        Console.SetCursorPosition(0, Console.CursorTop);
        Console.Write(new string(' ', Console.WindowWidth));
        Console.SetCursorPosition(0, currentLineCursor);
    }
}