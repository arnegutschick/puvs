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
        try
        {
            Console.WriteLine("ChatServer is starting...");

            // AppContext.BaseDirectory points to bin/Debug/net10.0/
            // Move 4 levels up to reach the project/repo root
            string repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
            string configPath = Path.Combine(repoRoot, "appsettings.json");

            var configuration = new ConfigurationBuilder()
                .SetBasePath(repoRoot)
                .AddJsonFile(configPath, optional: false, reloadOnChange: true)
                .Build();

            // --- RabbitMQ Bus Connection ---
            string rabbitHost = configuration.GetValue<string>("RabbitMQ:Host") ?? "localhost";
            using IBus bus = await RabbitMqBusFactory.CreateAndValidateAsync($"host={rabbitHost}");

            Console.WriteLine($"Connected to RabbitMQ at {rabbitHost}.");

            // Load color pool for chat messages from config
            string[] colorPool = configuration.GetSection("ChatSettings:ChatMessageColorPool").Get<string[]>() ?? Array.Empty<string>();
            if (colorPool.Length == 0)
            {
                Console.WriteLine("[WARNING] No colors configured. Using default colors.");
                colorPool = ["Blue", "Green", "Magenta", "Cyan"];
            }

            // Dictionary to track connected users
            var connectedUsers = new ConcurrentDictionary<string, UserInfo>(StringComparer.OrdinalIgnoreCase);

            // Core services
            var userService = new UserService(connectedUsers, colorPool);
            var statisticsService = new StatisticsService();
            var messageService = new MessageService(connectedUsers, statisticsService);
            var privateMessageService = new PrivateMessageService(connectedUsers);
            var fileService = new FileService();
            var heartbeatService = new HeartbeatService(userService, bus, configuration);

            // Handlers subscribe to Pub/Sub events and process incoming messages/commands
            var userHandler = new UserHandler(bus, userService);
            var messageHandler = new MessageHandler(bus, messageService);
            var privateMessageHandler = new PrivateMessageHandler(bus, privateMessageService);
            var fileHandler = new FileHandler(bus, fileService);
            var heartbeatHandler = new HeartbeatHandler(bus, heartbeatService);
            var statisticsHandler = new StatisticsHandler(bus, statisticsService);
            var timeHandler = new TimeHandler(bus);

            // Start listening to events asynchronously
            await userHandler.StartAsync();
            await messageHandler.StartAsync();
            await privateMessageHandler.StartAsync();
            await fileHandler.StartAsync();
            await heartbeatHandler.StartAsync();
            await statisticsHandler.StartAsync();
            await timeHandler.StartAsync();

            // Start server heartbeat publisher
            heartbeatService.StartServerHeartbeat();

            // Periodically removes inactive users
            using var cts = new CancellationTokenSource();
            _ = Task.Run(() => heartbeatService.StartCleanupTask(cts.Token));

            Console.WriteLine("Server is running. Press [Enter] to exit.");
            Console.ReadLine();

            Console.WriteLine("ChatServer is shutting down.");

            // Cancel removal task
            cts.Cancel();

            // Stop Server Heartbeat
            heartbeatService.StopServerHeartbeat();

            bus.Dispose();
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine($"[FATAL] Server failed: {ex.Message}");
            return;
        }
        catch (Exception)
        {
            Console.WriteLine("[FATAL] Server failed.");
            return;
        }
    }
}