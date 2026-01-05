using Chat.Contracts;
using Chat.Contracts.Infrastructure;
using EasyNetQ;

namespace ChatServer.Handlers;

/// <summary>
/// Handler for server time requests via RPC.
/// Responds with the current server time when a <see cref="TimeRequest"/> is received.
/// </summary>
public class TimeHandler
{
    private readonly IBus _bus;

    /// <summary>
    /// Initializes the TimeHandler with the message bus.
    /// </summary>
    /// <param name="bus">The EasyNetQ message bus for RPC communication.</param>
    public TimeHandler(IBus bus)
    {
        _bus = bus;
    }


    /// <summary>
    /// Starts the RPC responder for <see cref="TimeRequest"/>.
    /// Clients calling this RPC will receive a <see cref="TimeResponse"/> with the current server time.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous subscription registration.</returns>
    public async Task StartAsync()
    {
        try
        {
            // Register RPC handler for TimeRequest → TimeResponse
            await _bus.Rpc.RespondAsync<TimeRequest, TimeResponse>(HandleAsync);
        }
        catch (Exception)
        {
            Console.WriteLine($"[ERROR] Failed to start TimeHandler RPC. Maybe RabbitMQ is down?");
        }
    }


    /// <summary>
    /// Handles an incoming <see cref="TimeRequest"/> and returns the current server time.
    /// </summary>
    /// <param name="request">The incoming time request (currently unused).</param>
    /// <returns>A <see cref="Task{TimeResponse}"/> containing the current server time.</returns>
    private async Task<TimeResponse> HandleAsync(TimeRequest request)
    {
        if (request == null)
        {
            Console.WriteLine("[WARNING] Received null TimeRequest");
            return new TimeResponse(false, DateTime.MinValue);
        }

        try
        {
            Console.WriteLine("Received time request.");

            var response = new TimeResponse(true, DateTime.Now);

            Console.WriteLine("Sent time response.");
            return response;
        }
        catch (Exception ex)
        {
            // Log the error on the server
            Console.WriteLine($"[ERROR] Failed to process time request: {ex.Message}");

            // Notify the requesting client via ErrorEvent
            if (!string.IsNullOrWhiteSpace(request.SenderUsername))
            {
                try
                {
                    string senderTopic = TopicNames.CreatePrivateUserTopicName(request.SenderUsername);
                    var errorEvent = new ErrorEvent($"Failed to retrieve server time. Please try again.");
                    await _bus.PubSub.PublishAsync(errorEvent, senderTopic);
                }
                catch (Exception innerEx)
                {
                    Console.Error.WriteLine($"[ERROR] Failed to send ErrorEvent to '{request.SenderUsername}': {innerEx}");
                }
            }

            // Return a fallback response
            return new TimeResponse(false, DateTime.MinValue);
        }
    }
}