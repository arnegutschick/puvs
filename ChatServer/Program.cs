using EasyNetQ;
using ChatServer.Infrastructure;
using ChatServer.Services;
using ChatServer.Handlers;
using System.Collections.Concurrent;

namespace ChatServer;

internal class Program
{
    private static readonly ConcurrentDictionary<string, UserInfo> ConnectedUsers = new(StringComparer.OrdinalIgnoreCase);

    private static readonly string[] ColorPool = new[]
    {
        "Blue", "Green", "Magenta", "Cyan", "Brown",
        "BrightBlue", "BrightMagenta", "BrightCyan", "BrightRed"
    };

    private static async Task Main(string[] args)
    {
        Console.WriteLine("ChatServer is starting...");

        try
        {
            // --- Bus connection ---
            using IBus bus = RabbitMqBusFactory.Create("host=localhost");
            Console.WriteLine("Connected to RabbitMQ.");

            // --- Services ---
            var userService = new UserService(ConnectedUsers, ColorPool);
            var statisticsService = new StatisticsService();
            var messageService = new MessageService(bus, ConnectedUsers, statisticsService);
            var privateMessageService = new PrivateMessageService(bus, ConnectedUsers);
            var fileService = new FileService(bus);
            var heartbeatService = new HeartbeatService(userService, bus);

            // --- Handlers ---
            var userHandler = new UserHandler(bus, userService);                  // Login / Logout
            var messageHandler = new MessageHandler(bus, messageService);         // Broadcast messages
            var privateMessageHandler = new PrivateMessageHandler(bus, privateMessageService); // /msg
            var fileHandler = new FileHandler(bus, fileService);                  // /sendfile
            var heartbeatHandler = new HeartbeatHandler(bus, heartbeatService);   // Heartbeats
            var statisticsHandler = new StatisticsHandler(bus, statisticsService); // /stats RPC
            var timeHandler = new TimeHandler(bus);                                // /time RPC

            // --- Start Handlers ---
            userHandler.Start();
            await messageHandler.StartAsync();
            await privateMessageHandler.StartAsync();
            await fileHandler.StartAsync();
            await heartbeatHandler.StartAsync();
            await statisticsHandler.StartAsync();
            await timeHandler.StartAsync();

            // --- Heartbeat cleanup loop ---
            heartbeatService.StartCleanupTask();

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
