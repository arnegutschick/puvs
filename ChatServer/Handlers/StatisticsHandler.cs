using Chat.Contracts;
using ChatServer.Services;
using EasyNetQ;

namespace ChatServer.Handlers;

public class StatisticsHandler
{
    private readonly IBus _bus;
    private readonly StatisticsService _service;

    public StatisticsHandler(IBus bus, StatisticsService service)
    {
        _bus = bus;
        _service = service;
    }

    public Task StartAsync()
    {
        return _bus.Rpc.RespondAsync<StatisticsRequest, StatisticsResponse>(HandleAsync);
    }

    private Task<StatisticsResponse> HandleAsync(StatisticsRequest request)
    {
        var (total, avg, top3) = _service.Snapshot();

        var response = new StatisticsResponse(
            TotalMessages: total,
            AvgMessagesPerUser: avg,
            Top3: top3.Select(t => new TopChatterDto(t.User, t.MessageCount)).ToList()
        );

        return Task.FromResult(response);
    }
}
