using Chat.Contracts;
using EasyNetQ;

namespace ChatServer.Handlers;

public class TimeHandler
{
    private readonly IBus _bus;

    public TimeHandler(IBus bus)
    {
        _bus = bus;
    }

    public Task StartAsync()
    {
        return _bus.Rpc.RespondAsync<TimeRequest, TimeResponse>(HandleAsync);
    }

    private Task<TimeResponse> HandleAsync(TimeRequest request)
    {
        return Task.FromResult(new TimeResponse(DateTime.Now));
    }
}
