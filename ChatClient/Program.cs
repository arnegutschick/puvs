using Chat.Contracts.Infrastructure;
using EasyNetQ;
using Microsoft.Extensions.Configuration;

namespace ChatClient;

internal static class Program
{
    static void Main()
    {
        try
        {
            // --- Configuration Setup ---
            // Resolve the repository root directory
            string repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
            // Load appsettings.json from repo root
            var configFile = Path.Combine(repoRoot, "appsettings.json");
            var configuration = new ConfigurationBuilder()
                .AddJsonFile(configFile, optional: false, reloadOnChange: true)
                .Build();

            // --- Bus Connection ---
            // Get RabbitMQ host from configuration (default: localhost)
            string rabbitHost = configuration.GetValue<string>("RabbitMQ:Host") ?? "localhost";
            // Create EasyNetQ bus instance for Pub/Sub and RPC
            IBus bus = RabbitMqBusFactory.Create($"host={rabbitHost}");

            // --- User Login ---
            Console.Write("Enter username: ");
            string username = Console.ReadLine() ?? Guid.NewGuid().ToString();

            // Perform login via RPC call
            var loginResponse = bus.Rpc.Request<Chat.Contracts.LoginRequest, Chat.Contracts.LoginResponse>(
                new Chat.Contracts.LoginRequest(username)
            );

            if (!loginResponse.IsSuccess)
            {
                Console.WriteLine($"Login failed: {loginResponse.Reason}");
                return; // Exit program if login fails
            }

            // --- Initialize UI and Logic ---
            ChatUI ui = null!; // Placeholder, will be assigned after logic is created

            // Create logic instance first, assign callbacks to UI methods
            ChatLogic logic = new ChatLogic(
                bus,
                username,
                configuration,
                // Callback for appending messages to UI
                (msg, color) => ui?.AppendMessage(msg, color),
                // Callback for showing file save dialog
                file => ui?.ShowSaveFileDialog(file) ?? new Terminal.Gui.Dialog()
            );

            // Create UI instance and pass the logic instance
            ui = new ChatUI(logic);

            // --- Run the Terminal GUI ---
            ui.Run(); // Blocks until application exits

            // --- Cleanup on Exit ---
            // Publish logout event to notify server
            bus.PubSub.Publish(new Chat.Contracts.LogoutRequest(username));
            // Dispose the bus to free resources
            bus.Dispose();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FATAL] Client failed: {ex}");
        }
    }
}
