using Chat.Contracts.Infrastructure;
using EasyNetQ;
using EasyNetQ.Topology;
using Microsoft.Extensions.Configuration;

namespace ChatClient;

internal static class Program
{
    static async Task Main()
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

            // RabbitMQ ping simulation: try to create a connection
            try
            {
                var advancedBus = bus.Advanced;
                var testExchange = await advancedBus.ExchangeDeclareAsync("ping_check", ExchangeType.Fanout);
                await advancedBus.ExchangeDeleteAsync(testExchange);

                // If no exception occurs the connection seems stable
                Console.WriteLine($"Connected to RabbitMQ at {rabbitHost}.");
            }
            catch (Exception)
            {
                Console.WriteLine($"[ERROR] Cannot reach RabbitMQ at {rabbitHost}.");
                Environment.Exit(1);
            }


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
        catch (Exception)
        {
            Console.WriteLine($"[FATAL] Client failed.");
        }
    }
}
