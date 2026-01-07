using Chat.Contracts.Infrastructure;
using EasyNetQ;
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
            using IBus bus = await RabbitMqBusFactory.CreateAndValidateAsync($"host={rabbitHost}");

            Console.WriteLine($"Connected to RabbitMQ at {rabbitHost}.");

            // --- User Login ---
            Console.Write("Enter username: ");
            string username = Console.ReadLine() ?? Guid.NewGuid().ToString();

            // Use LoginService to perform RPC login
            var loginService = new Services.LoginService(bus);

            var loginResponse = await loginService.LoginAsync(username);
            if (loginResponse is null)
            {
                // Communication failure already reported by the service
                return;
            }

            if (!loginResponse.IsSuccess)
            {
                Console.WriteLine($"Login failed: {loginResponse.Reason}");
                return;
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
            // Publish logout event to notify server via LoginService - using fire-and-forget to prevent UI freeze
            // if this task fails, the server would register the timeout and perform a safe logout anyway
            _ = Task.Run(async () =>
            {
                try
                {
                    await loginService.LogoutAsync(username);
                }
                catch (Exception)
                {
                    Console.WriteLine($"[WARNING] Logout failed.");
                }
            });

        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine($"[FATAL] Client failed: {ex.Message}");
            return;
        }
        catch (Exception)
        {
            Console.WriteLine($"[FATAL] Client failed.");
            return;
        }
    }
}
