using Chat.Contracts;
using ChatServer.Services;
using EasyNetQ;

namespace ChatServer.Handlers;

/// <summary>
/// Handler for statistics requests via RPC.
/// Responds with a snapshot of chat statistics including total messages,
/// average messages per user, and top 3 most active users.
/// </summary>
public class StatisticsHandler
{
    private readonly IBus _bus;
    private readonly StatisticsService _service;

    /// <summary>
    /// Initializes the StatisticsHandler with a message bus and statistics service.
    /// </summary>
    /// <param name="bus">The EasyNetQ message bus for RPC communication.</param>
    /// <param name="service">The StatisticsService used to generate chat statistics.</param>
    public StatisticsHandler(IBus bus, StatisticsService service)
    {
        _bus = bus;
        _service = service;
    }


    /// <summary>
    /// Starts the RPC responder for <see cref="StatisticsRequest"/>.
    /// Clients calling this RPC will receive a <see cref="StatisticsResponse"/> with chat statistics.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous registration of the RPC handler.</returns>
    public Task StartAsync()
    {
        // Register RPC handler for StatisticsRequest → StatisticsResponse
        return _bus.Rpc.RespondAsync<StatisticsRequest, StatisticsResponse>(HandleAsync);
    }


    /// <summary>
    /// Handles an incoming <see cref="StatisticsRequest"/> and returns a snapshot of chat statistics.
    /// </summary>
    /// <param name="request">The statistics request containing the requesting user's username.</param>
    /// <returns>A <see cref="Task{StatisticsResponse}"/> containing the total messages,
    /// average messages per user, and the top 3 most active users.</returns>
    private Task<StatisticsResponse> HandleAsync(StatisticsRequest request)
    {
        Console.WriteLine($"Received statistics request from {request.RequestingUser}.");

        // Get the statistics snapshot
        var (total, avg, top3) = _service.Snapshot();

        // Build the response DTO
        var response = new StatisticsResponse(
            TotalMessages: total,
            AvgMessagesPerUser: avg,
            Top3: top3.Select(t => new TopChatterDto(t.User, t.MessageCount)).ToList()
        );

        Console.WriteLine($"Sent statistics response to {request.RequestingUser}.");

        return Task.FromResult(response);
    }
}
