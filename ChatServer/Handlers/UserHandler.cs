using Chat.Contracts;
using Chat.Contracts.Infrastructure;
using ChatServer.Services;
using EasyNetQ;

namespace ChatServer.Handlers;

/// <summary>
/// Handler for user-related events, including login and logout.
/// Registers RPC and Pub/Sub handlers that delegate work to <see cref="UserService"/>.
/// </summary>
public class UserHandler : CommandExecutionWrapper
{
    private readonly UserService _service;

    // Subscription ID for logout events
    private const string LogoutSubscriptionId = TopicNames.LogoutSubscription;

    /// <summary>
    /// Initializes the UserHandler with a message bus and the user service.
    /// </summary>
    /// <param name="bus">The EasyNetQ message bus for RPC and Pub/Sub communication.</param>
    /// <param name="service">The UserService responsible for managing user state.</param>
    public UserHandler(IBus bus, UserService service)
        : base(bus)
    {
        _service = service;
    }


    /// <summary>
    /// Starts the login and logout handlers.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous subscription registration.</returns>
    public async Task StartAsync()
    {
        await SafeStartAsync(StartLogin, "login RPC");
        await SafeStartAsync(StartLogout, "logout RPC");
        await SafeStartAsync(StartUserListRpc, "user list RPC");
    }


    /// <summary>
    /// Executes a start function with standardized try/catch and critical logging.
    /// </summary>
    /// <param name="startFunc">The start function to execute asynchronously.</param>
    /// <param name="description">Description for logging (e.g., "login RPC").</param>
    private async Task SafeStartAsync(Func<Task> startFunc, string description)
    {
        try
        {
            await startFunc();
        }
        catch (Exception)
        {
            Console.WriteLine($"[CRITICAL] Failed to start {description}. Maybe RabbitMQ is down?");
            throw;
        }
    }


    /// <summary>
    /// Registers the RPC responder for login requests.
    /// Delegates handling to <see cref="UserService.HandleLoginAsync"/>.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous subscription registration.</returns>
    private Task StartLogin()
    {
        return Bus.Rpc.RespondAsync<LoginRequest, LoginResponse>(request =>
        {
            if (request == null)
            {
                Console.WriteLine("[WARNING] Received null LoginRequest");
                return Task.FromResult(new LoginResponse(false, "Invalid request"));
            }

            return ExecuteRpcAsync(
                request.Username,
                () => Task.FromResult(_service.HandleLogin(request)),
                defaultResponse: new LoginResponse(false, "Login failed due to server error"),
                errorMessage: "Login failed. Please try again."
            );
        });
    }


    /// <summary>
    /// Subscribes to logout events via Pub/Sub.
    /// Delegates handling to <see cref="UserService.HandleLogoutAsync"/>.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous subscription registration.</returns>
    private Task StartLogout()
    {
        return Bus.PubSub.SubscribeAsync<LogoutRequest>(
            LogoutSubscriptionId,
            request =>
            {
                if (request == null)
                {
                    Console.WriteLine("[WARNING] Received null LogoutRequest");
                    return Task.CompletedTask;
                }

                return ExecuteCommandAsync(
                    request.Username,
                    () => 
                    {
                        _service.RemoveUser(request.Username);
                        return Task.CompletedTask;
                    },
                    errorMessage: "Logout failed. Please try again."
                );
            }
        );
    }


    /// <summary>
    /// Registers an RPC responder for <see cref="UserListRequest"/>.
    /// Delegates to <see cref="UserService.GetActiveUsers"/> to retrieve the current list of active users.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous subscription registration.</returns>
    private Task StartUserListRpc()
    {
        return Bus.Rpc.RespondAsync<UserListRequest, UserListResponse>(request =>
        {
            if (request == null)
            {
                Console.WriteLine("[WARNING] Received null UserListRequest");
                return Task.FromResult(new UserListResponse(false, Array.Empty<string>()));
            }

            return ExecuteRpcAsync(
                username: request.SenderUsername,
                handlerAction: async () =>
                {
                    // Retrieve active users from the service
                    var users = _service.GetActiveUsers();
                    return new UserListResponse(true, users);
                },
                defaultResponse: new UserListResponse(false, Array.Empty<string>()),
                errorMessage: "Failed to retrieve user list. Please try again."
            );
        });
    }
}