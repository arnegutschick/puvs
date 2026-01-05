using Chat.Contracts;
using Chat.Contracts.Infrastructure;
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
    /// <returns>A <see cref="Task"/> representing the asynchronous subscription registration.</returns>
    public async Task StartAsync()
    {
        try
        {
            // Register RPC handler for StatisticsRequest → StatisticsResponse
            await _bus.Rpc.RespondAsync<StatisticsRequest, StatisticsResponse>(HandleAsync);
        }
        catch (Exception)
        {
            Console.WriteLine($"[ERROR] Failed to start statistics RPC. Maybe RabbitMQ is down?");
        }
    }


    /// <summary>
    /// Handles an incoming <see cref="StatisticsRequest"/> and returns a snapshot of chat statistics.
    /// </summary>
    /// <param name="request">The statistics request containing the requesting user's username.</param>
    /// <returns>A <see cref="Task{StatisticsResponse}"/> containing the total messages,
    /// average messages per user, and the top 3 most active users.</returns>
    private async Task<StatisticsResponse> HandleAsync(StatisticsRequest request)
    {
        if (request == null)
        {
            Console.WriteLine("[WARNING] Received null StatisticsRequest");
            return new StatisticsResponse(false, 0, 0, new List<TopChatterDto>());
        }

        try
        {
            Console.WriteLine($"Received statistics request from {request.RequestingUser}.");

            // Get the statistics snapshot
            var (total, avg, top3) = _service.Snapshot();

            // Build the response DTO
            var response = new StatisticsResponse(
                IsSuccess: true,
                TotalMessages: total,
                AvgMessagesPerUser: avg,
                Top3: top3.Select(t => new TopChatterDto(t.UserName, t.MessageCount)).ToList()
            );

            Console.WriteLine($"Sent statistics response to {request.RequestingUser}.");

            return response;
        }
        catch (Exception ex)
        {
            // Log the error on the server
            Console.WriteLine(
                $"[ERROR] Failed to process statistics request from '{request.RequestingUser}': {ex.Message}"
            );

            // Notify the requesting user about the failure via ErrorEvent
            try
            {
                string senderTopic = TopicNames.CreatePrivateUserTopicName(request.RequestingUser);
                var errorEvent = new ErrorEvent($"Failed to retrieve statistics. Please try again.");
                await _bus.PubSub.PublishAsync(errorEvent, senderTopic);
            }
            catch (Exception innerEx)
            {
                Console.Error.WriteLine($"[ERROR] Failed to send ErrorEvent to '{request.RequestingUser}': {innerEx}");
            }

            // Return empty/default statistics to avoid crashing the RPC call
            return new StatisticsResponse(false, 0, 0, new List<TopChatterDto>());
        }
    }
}
