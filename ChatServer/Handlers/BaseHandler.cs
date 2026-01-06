using Chat.Contracts;
using Chat.Contracts.Infrastructure;
using EasyNetQ;

namespace ChatServer.Handlers;

/// <summary>
/// Base class for all handlers (commands and RPC) providing
/// standardized error handling and client notification.
/// </summary>
public abstract class BaseHandler
{
    protected readonly IBus Bus;

    protected BaseHandler(IBus bus)
    {
        Bus = bus;
    }

    /// <summary>
    /// Executes an asynchronous command with standardized error handling.
    /// If an exception occurs, it is logged and the sender is notified via <see cref="ErrorEvent"/>.
    /// </summary>
    /// <param name="username">The username of the sender performing the command.</param>
    /// <param name="handlerAction">The asynchronous function representing the actual command logic.</param>
    /// <param name="errorMessage">
    /// Optional user-friendly error message to send via <see cref="ErrorEvent"/> if the action fails.
    /// Defaults to a generic error message.
    /// </param>
    /// <returns>A <see cref="Task"/> that completes when the command has finished execution.</returns>
    protected async Task ExecuteCommandAsync(
        string username,
        Func<Task> handlerAction,
        string? errorMessage = null)
    {
        try
        {
            await handlerAction();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Command execution failed: {ex.Message}");

            await TryNotifyUserAsync(username, errorMessage ?? "An error occurred. Please try again.");
        }
    }


    /// <summary>
    /// Executes an asynchronous RPC handler with standardized error handling.
    /// If an exception occurs, it is logged and the requester is notified via <see cref="ErrorEvent"/>.
    /// </summary>
    /// <typeparam name="TResponse">The type of the response returned by the RPC.</typeparam>
    /// <param name="username">The username of the requester.</param>
    /// <param name="handlerAction">The asynchronous function representing the RPC logic.</param>
    /// <param name="defaultResponse">
    /// The response to return if an exception occurs. Ensures the RPC call always returns a value.
    /// </param>
    /// <param name="errorMessage">
    /// Optional user-friendly error message to send via <see cref="ErrorEvent"/> if the RPC fails.
    /// Defaults to a generic error message.
    /// </param>
    /// <returns>
    /// A <see cref="Task{TResult}"/> that completes with the RPC result, or <paramref name="defaultResponse"/> if an error occurs.
    /// </returns>
    protected async Task<TResponse> ExecuteRpcAsync<TResponse>(
        string username,
        Func<Task<TResponse>> handlerAction,
        TResponse defaultResponse,
        string? errorMessage = null)
    {
        try
        {
            return await handlerAction();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] RPC execution failed: {ex.Message}");

            await TryNotifyUserAsync(username, errorMessage ?? "An error occurred. Please try again.");

            return defaultResponse;
        }
    }


    /// <summary>
    /// Sends an <see cref="ErrorEvent"/> message to the specified user.
    /// Best-effort only; any exceptions during notification are logged but not rethrown.
    /// </summary>
    /// <param name="username">The username of the recipient.</param>
    /// <param name="message">The error message to send to the user.</param>
    private async Task TryNotifyUserAsync(string username, string message)
    {
        try
        {
            string topic = TopicNames.CreatePrivateUserTopicName(username);
            await Bus.PubSub.PublishAsync(new ErrorEvent(message), topic);
        }
        catch (Exception innerEx)
        {
            Console.Error.WriteLine(
                $"[ERROR] Failed to send ErrorEvent to '{username}': {innerEx}");
        }
    }
}
