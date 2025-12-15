using EasyNetQ;
using Chat.Contracts;

namespace ChatServer;

internal class Program
{
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
                Console.WriteLine($"Login request for user: '{request.Username}'");
                if (string.IsNullOrWhiteSpace(request.Username))
                {
                    return new LoginResponse(false, "Username cannot be empty.");
                }

                Console.WriteLine($"User '{request.Username}' logged in successfully.");

                // Announce the new user to all clients
                await bus.PubSub.PublishAsync(new UserNotification($"*** User '{request.Username}' has joined the chat. ***"));

                return new LoginResponse(true, string.Empty);
            });
            // --- RPC: Handle /time request ---
            await bus.Rpc.RespondAsync<TimeRequest, TimeResponse>(request =>
            {
                return Task.FromResult(new TimeResponse(DateTime.Now));
            });
            // --- Pub/Sub: Handle incoming commands from clients to submit a message ---
            await bus.PubSub.SubscribeAsync<SubmitMessageCommand>("chat_server_submit_message_subscription", async command =>
            {
                Console.WriteLine($"Received message from '{command.Username}': '{command.Text}'");

                // Create the event that will be broadcast to all clients
                BroadcastMessageEvent broadcastEvent = new BroadcastMessageEvent(command.Username, command.Text);

                // Broadcast the event to all clients
                await bus.PubSub.PublishAsync(broadcastEvent);
                Console.WriteLine($"Broadcasted message to all clients.");
            });

            // --- Pub/Sub: Handle Logout Requests ---
            await bus.PubSub.SubscribeAsync<LogoutRequest>("chat_server_logout_subscription", async request =>
            {
                Console.WriteLine($"Logout request for user: '{request.Username}'");

                Console.WriteLine($"User '{request.Username}' logged out.");
                // Announce the departure to all clients
                await bus.PubSub.PublishAsync(new UserNotification($"*** User '{request.Username}' has left the chat. ***"));

            });

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
}