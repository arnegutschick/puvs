using Chat.Contracts;
using EasyNetQ;

namespace ChatServer.Services;

public class HeartbeatService
{
    private readonly UserService _users;
    private readonly IBus _bus;
    private readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    public HeartbeatService(UserService users, IBus bus)
    {
        _users = users;
        _bus = bus;
    }

    public void Handle(string username)
    {
        _users.UpdateHeartbeat(username);
    }

    public void StartCleanupTask(CancellationToken token = default)
    {
        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(_timeout, token);

                var timedOut = _users.GetTimedOut(_timeout);

                foreach (var username in timedOut)
                {
                    if (_users.Remove(username))
                    {
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
