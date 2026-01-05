using Chat.Contracts;
using Chat.Contracts.Infrastructure;
using ChatServer.Services;
using EasyNetQ;

namespace ChatServer.Handlers;

/// <summary>
/// Handler for user-related events, including login and logout.
/// Registers RPC and Pub/Sub handlers that delegate work to <see cref="UserService"/>.
/// </summary>
public class UserHandler
{
    private readonly IBus _bus;
    private readonly UserService _service;

    // Subscription ID for logout events
    private const string LogoutSubscriptionId = TopicNames.LogoutSubscription;

    /// <summary>
    /// Initializes the UserHandler with a message bus and the user service.
    /// </summary>
    /// <param name="bus">The EasyNetQ message bus for RPC and Pub/Sub communication.</param>
    /// <param name="service">The UserService responsible for managing user state.</param>
    public UserHandler(IBus bus, UserService service)
    {
        _bus = bus;
        _service = service;
    }


    /// <summary>
    /// Starts the login and logout handlers.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous subscription registration.</returns>
    public async Task StartAsync()
    {
        try
        {
            await StartLogin();
        }
        catch (Exception)
        {
            Console.WriteLine($"[CRITICAL] Failed to start login RPC. Maybe RabbitMQ is down?");
        }

        try
        {
            await StartLogout();
        }
        catch (Exception)
        {
            Console.WriteLine($"[CRITICAL] Failed to start logout RPC. Maybe RabbitMQ is down?");
        }

        try
        {
            await StartUserListRpc();
        }
        catch (Exception)
        {
            Console.WriteLine($"[CRITICAL] Failed to start user list RPC. Maybe RabbitMQ is down?");
        }
    }


    /// <summary>
    /// Registers the RPC responder for login requests.
    /// Delegates handling to <see cref="UserService.HandleLoginAsync"/>.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous subscription registration.</returns>
    private async Task StartLogin()
    {
        await _bus.Rpc.RespondAsync<LoginRequest, LoginResponse>(async request =>
        {
            if (request == null)
            {
                Console.WriteLine("[WARNING] Received null LoginRequest");
                return new LoginResponse(false, "Invalid request");
            }

            try
            {
                return await _service.HandleLoginAsync(request, _bus);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to process login for '{request.Username}': {ex.Message}");

                // Notify the client about the failure
                try
                {
                    string senderTopic = TopicNames.CreatePrivateUserTopicName(request.Username);
                    var errorEvent = new ErrorEvent($"Login failed. Please try again.");
                    await _bus.PubSub.PublishAsync(errorEvent, senderTopic);
                }
                catch (Exception innerEx)
                {
                    Console.Error.WriteLine($"[ERROR] Failed to send ErrorEvent to '{request.Username}': {innerEx}");
                }

                return new LoginResponse(false, "Login failed due to server error");
            }
        });
    }


    /// <summary>
    /// Subscribes to logout events via Pub/Sub.
    /// Delegates handling to <see cref="UserService.HandleLogoutAsync"/>.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous subscription registration.</returns>
    private async Task StartLogout()
    {
        await _bus.PubSub.SubscribeAsync<LogoutRequest>(
            LogoutSubscriptionId,
            async request =>
            {
                if (request == null)
                {
                    Console.WriteLine("[WARNING] Received null LogoutRequest");
                    return;
                }

                try
                {
                    await _service.HandleLogoutAsync(request, _bus);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Failed to process logout for '{request.Username}': {ex.Message}");

                    // Notify the client about the failure
                    try
                    {
                        string senderTopic = TopicNames.CreatePrivateUserTopicName(request.Username);
                        var errorEvent = new ErrorEvent($"Logout failed. Please try again.");
                        await _bus.PubSub.PublishAsync(errorEvent, senderTopic);
                    }
                    catch (Exception innerEx)
                    {
                        Console.Error.WriteLine($"[ERROR] Failed to send ErrorEvent to '{request.Username}': {innerEx}");
                    }
                }
            }
        );
    }


    /// <summary>
    /// Registers an RPC responder for <see cref="UserListRequest"/>.
    /// Delegates to <see cref="UserService.GetActiveUsers"/> to retrieve the current list of active users.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous subscription registration.</returns>
    private async Task StartUserListRpc()
    {
        await _bus.Rpc.RespondAsync<UserListRequest, UserListResponse>(async request =>
        {
            if (request == null)
            {
                Console.WriteLine("[WARNING] Received null UserListRequest");
                return new UserListResponse(false, Array.Empty<string>());
            }

            try
            {
                // Retrieve active users from the service
                var users = _service.GetActiveUsers();
                return new UserListResponse(true, users);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to process UserListRequest: {ex.Message}");

                // Notify the client about the failure
                try
                {
                    string senderTopic = TopicNames.CreatePrivateUserTopicName(request.SenderUsername);
                    var errorEvent = new ErrorEvent($"Failed to retrieve user list. Please try again.");
                    await _bus.PubSub.PublishAsync(errorEvent, senderTopic);
                }
                catch (Exception innerEx)
                {
                    Console.WriteLine($"[ERROR] Failed to send ErrorEvent to '{request.SenderUsername}': {innerEx}");
                }

                return new UserListResponse(false, Array.Empty<string>());
            }
        });
    }
}