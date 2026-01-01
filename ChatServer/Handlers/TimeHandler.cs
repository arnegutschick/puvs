using Chat.Contracts;
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
    /// <returns>A <see cref="Task"/> representing the asynchronous registration of the RPC handler.</returns>
    public Task StartAsync()
    {
        // Register RPC handler for TimeRequest → TimeResponse
        return _bus.Rpc.RespondAsync<TimeRequest, TimeResponse>(HandleAsync);
    }


    /// <summary>
    /// Handles an incoming <see cref="TimeRequest"/> and returns the current server time.
    /// </summary>
    /// <param name="request">The incoming time request (currently unused).</param>
    /// <returns>A <see cref="Task{TimeResponse}"/> containing the current server time.</returns>
    private Task<TimeResponse> HandleAsync(TimeRequest request)
    {
        // Return current server time immediately
        return Task.FromResult(new TimeResponse(DateTime.Now));
    }
}