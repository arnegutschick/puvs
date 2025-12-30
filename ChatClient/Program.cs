using EasyNetQ;

namespace ChatClient;

internal static class Program
{
    private static IBus bus = null!;
    private static string username = "";

    static void Main()
    {
        // Create RabbitMQ bus
        bus = RabbitHutch.CreateBus("host=localhost");

        // Ask user for username
        Console.Write("Enter username: ");
        username = Console.ReadLine() ?? Guid.NewGuid().ToString();

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
            // Callback for appending messages to UI
            msg => ui?.AppendMessage(msg),
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
