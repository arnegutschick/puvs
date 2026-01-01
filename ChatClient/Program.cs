using Chat.Contracts.Infrastructure;
using EasyNetQ;
using Microsoft.Extensions.Configuration;

namespace ChatClient;

internal static class Program
{
    private static readonly IConfiguration configuration = new ConfigurationBuilder()
        .SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
        .Build();

    static void Main()
    {
        // --- Bus connection ---
        string rabbitHost = configuration.GetValue<string>("RabbitMQ:Host") ?? "localhost";
        int rabbitPort = configuration.GetValue<int>("RabbitMQ:Port");

        IBus bus = RabbitMqBusFactory.Create($"host={rabbitHost};port={rabbitPort}");

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