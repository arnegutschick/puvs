using Chat.Contracts;
using EasyNetQ;
using Microsoft.Extensions.Configuration;

namespace ChatServer.Services;

public class HeartbeatService
{
    private readonly UserService _users;
    private readonly IBus _bus;
    private readonly TimeSpan _timeout;

    public HeartbeatService(UserService users, IBus bus, IConfiguration configuration)
    {
        _users = users;
        _bus = bus;
        int timeoutSeconds = configuration.GetValue("ChatSettings:HeartbeatTimeoutSeconds", 30);
        _timeout = TimeSpan.FromSeconds(timeoutSeconds);
    }

    public void Handle(string username)
    {
        _users.UpdateHeartbeat(username);
    }

    public Task StartCleanupTask(CancellationToken token = default)
    {
        return Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(_timeout, token);

                var timedOut = _users.GetTimedOut(_timeout);

                foreach (var username in timedOut)
                {
                    if (_users.Remove(username))
                    {
                        Console.WriteLine($"User '{username}' has timed out.");
                        await _bus.PubSub.PublishAsync(
                            new UserNotification(
                                $"*** User '{username}' has left the chat (timeout). ***"
                            )
                        );
                    }
                }
            }
        }, token);
    }
}
