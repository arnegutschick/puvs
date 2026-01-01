using Chat.Contracts.Infrastructure;
using EasyNetQ;
using Microsoft.Extensions.Configuration;

namespace ChatClient;

internal static class Program
{
    static void Main()
    {
        string repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var configFile = Path.Combine(repoRoot, "appsettings.json");
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(configFile, optional: false, reloadOnChange: true)
            .Build();

        // --- Bus connection ---
        string rabbitHost = configuration.GetValue<string>("RabbitMQ:Host") ?? "localhost";

        IBus bus = RabbitMqBusFactory.Create($"host={rabbitHost}");

        // Ask user for username
        Console.Write("Enter username: ");
        string username = Console.ReadLine() ?? Guid.NewGuid().ToString();

        // Perform login via RPC
        var loginResponse = bus.Rpc.Request<Chat.Contracts.LoginRequest, Chat.Contracts.LoginResponse>(
            new Chat.Contracts.LoginRequest(username)
        );

        if (!loginResponse.IsSuccess)
        {
            Console.WriteLine($"Login failed: {loginResponse.Reason}");
            return;
        }

        // Initialize UI and Logic
        ChatUI ui = null!;

        // Create logic instance first, assign callbacks to UI methods
        ChatLogic logic = new ChatLogic(
            bus,
            username,
            configuration,
            // Callback for appending messages to UI
            (msg, color) => ui?.AppendMessage(msg, color),
            // Callback for showing file dialog
            file => ui?.ShowSaveFileDialog(file) ?? new Terminal.Gui.Dialog()
        );

        // Create UI instance and pass logic
        ui = new ChatUI(logic);

        // Run the terminal GUI
        ui.Run();

        // Logout and dispose bus on exit
        bus.PubSub.Publish(new Chat.Contracts.LogoutRequest(username));
        bus.Dispose();
    }
}