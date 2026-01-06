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
public class StatisticsHandler : CommandExecutionWrapper
{
    private readonly StatisticsService _service;

    /// <summary>
    /// Initializes the StatisticsHandler with a message bus and statistics service.
    /// </summary>
    /// <param name="bus">The EasyNetQ message bus for RPC communication.</param>
    /// <param name="service">The StatisticsService used to generate chat statistics.</param>
    public StatisticsHandler(IBus bus, StatisticsService service)
        : base(bus)
    {
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
            await Bus.Rpc.RespondAsync<StatisticsRequest, StatisticsResponse>(HandleAsync);
        }
        catch (Exception)
        {
            Console.WriteLine($"[ERROR] Failed to start statistics RPC. Maybe RabbitMQ is down?");
            throw;
        }
    }


    /// <summary>
    /// Handles an incoming <see cref="StatisticsRequest"/> asynchronously.
    /// - Logs a warning and returns a default <see cref="StatisticsResponse"/> if the request is null
    /// - Retrieves message statistics including total messages, average messages per user, and top 3 chatters
    /// - Uses <see cref="ExecuteRpcAsync"/> to handle execution context, errors, and provide a default response
    /// </summary>
    /// <param name="request">The <see cref="StatisticsRequest"/> containing the requesting user's information.</param>
    /// <returns>
    /// A <see cref="Task{StatisticsResponse}"/> representing the asynchronous operation.
    /// The response contains success status, total messages, average messages per user, and a list of top chatters.
    /// </returns>
    private Task<StatisticsResponse> HandleAsync(StatisticsRequest request)
    {
        if (request == null)
        {
            Console.WriteLine("[WARNING] Received null StatisticsRequest");
            return Task.FromResult(
                new StatisticsResponse(false, 0, 0, new List<TopChatterDto>())
            );
        }

        return ExecuteRpcAsync(
            username: request.RequestingUser,
            handlerAction: async () =>
            {
                Console.WriteLine($"Received statistics request from {request.RequestingUser}.");

                // Call service to generate chat statistics
                var (total, avg, top3) = _service.Snapshot();

                // Create response
                var response = new StatisticsResponse(
                    IsSuccess: true,
                    TotalMessages: total,
                    AvgMessagesPerUser: avg,
                    Top3: top3.Select(t => new TopChatterDto(t.UserName, t.MessageCount)).ToList()
                );

                Console.WriteLine($"Sent statistics response to {request.RequestingUser}.");

                return response;
            },
            defaultResponse: new StatisticsResponse(false, 0, 0, new List<TopChatterDto>()),
            errorMessage: "Failed to retrieve statistics. Please try again."
        );
    }
}
