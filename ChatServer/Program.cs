using EasyNetQ;
using Chat.Contracts.Infrastructure;
using ChatServer.Services;
using ChatServer.Handlers;
using System.Collections.Concurrent;
using Microsoft.Extensions.Configuration;

namespace ChatServer;

internal class Program
{
    private static async Task Main(string[] args)
    {
        Console.WriteLine("ChatServer is starting...");

            // --- Config aus Repo-Wurzel laden ---
            // AppContext.BaseDirectory zeigt auf bin/Debug/net10.0/
            // Wir gehen 3 Ebenen hoch zum Projekt-/Repo-Root
            string repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
            string configPath = Path.Combine(repoRoot, "appsettings.json");

            var configuration = new ConfigurationBuilder()
                .SetBasePath(repoRoot)
                .AddJsonFile(configPath, optional: false, reloadOnChange: true)
                .Build();

            // --- Bus connection ---
            string rabbitHost = configuration.GetValue<string>("RabbitMQ:Host") ?? "localhost";

            using IBus bus = RabbitMqBusFactory.Create($"host={rabbitHost}");
            Console.WriteLine($"Connected to RabbitMQ at {rabbitHost}.");

            // --- Services ---
            var ColorPool = configuration.GetSection("ChatSettings:ChatMessageColorPool").Get<string[]>() ?? Array.Empty<string>();
            if (ColorPool.Length == 0)
            {
                Console.WriteLine("Warning: No colors configured. Using default colors.");
                ColorPool = new[] { "Blue", "Green", "Magenta", "Cyan" };
            }
            
            var ConnectedUsers = new ConcurrentDictionary<string, UserInfo>(StringComparer.OrdinalIgnoreCase);

            var userService = new UserService(ConnectedUsers, ColorPool);
            var statisticsService = new StatisticsService();
            var messageService = new MessageService(bus, ConnectedUsers, statisticsService);
            var privateMessageService = new PrivateMessageService(bus, ConnectedUsers);
            var fileService = new FileService(bus);
            var heartbeatService = new HeartbeatService(userService, bus, configuration);

            // --- Handlers ---
            var userHandler = new UserHandler(bus, userService);
            var messageHandler = new MessageHandler(bus, messageService);
            var privateMessageHandler = new PrivateMessageHandler(bus, privateMessageService);
            var fileHandler = new FileHandler(bus, fileService);
            var heartbeatHandler = new HeartbeatHandler(bus, heartbeatService);
            var statisticsHandler = new StatisticsHandler(bus, statisticsService);
            var timeHandler = new TimeHandler(bus);

            // --- Start Handlers ---
            userHandler.Start();
            await messageHandler.StartAsync();
            await privateMessageHandler.StartAsync();
            await fileHandler.StartAsync();
            await heartbeatHandler.StartAsync();
            await statisticsHandler.StartAsync();
            await timeHandler.StartAsync();

            // --- Heartbeat cleanup loop ---
            await heartbeatService.StartCleanupTask();

            Console.WriteLine("Server is running. Press [Enter] to exit.");
            Console.ReadLine();

        Console.WriteLine("ChatServer is shutting down.");
    }
}
