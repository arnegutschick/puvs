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
            Console.WriteLine("Use '/msg <username> <message>' to send private messages.");
            Console.WriteLine("-----------------------------------------------------------------");
            
            string subscriptionId = $"chat_client_{Guid.NewGuid()}";
            string privateTopic = $"private_{username}";

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

            // --- Subscribe to Private Messages (topic-based) ---
            bus.PubSub.Subscribe<PrivateMessageEvent>(privateTopic, privateMessage =>
            {
                ClearCurrentConsoleLine();
                Console.ForegroundColor = ConsoleColor.Magenta;
                
                if (privateMessage.IsOutgoing)
                {
                    // Message sent by this user - show as outgoing
                    Console.WriteLine($"[privat an {privateMessage.RecipientUsername}] {privateMessage.Text}");
                }
                else
                {
                    // Message received from another user
                    Console.WriteLine($"[privat von {privateMessage.SenderUsername}] {privateMessage.Text}");
                }
                
                Console.ResetColor();
                Console.Write("Your message: ");
            }, config => config.WithTopic(privateTopic));

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

                // Handle private message command: /msg <recipient> <message>
                if (input.StartsWith("/msg ", StringComparison.OrdinalIgnoreCase))
                {
                    string[] parts = input.Substring(5).Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                    
                    if (parts.Length < 2)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("Usage: /msg <username> <message>");
                        Console.ResetColor();
                        continue;
                    }

                    string recipientUsername = parts[0];
                    string privateMessage = parts[1];

                    if (recipientUsername.Equals(username, StringComparison.OrdinalIgnoreCase))
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("You cannot send a private message to yourself.");
                        Console.ResetColor();
                        continue;
                    }

                    SendPrivateMessageCommand privateCommand = new SendPrivateMessageCommand(username, recipientUsername, privateMessage);
                    bus.PubSub.Publish(privateCommand);
                    continue;
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