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
    public async Task StartAsync()
    {
        await StartLogin();
        await StartLogout();
    }


    /// <summary>
    /// Registers the RPC responder for login requests.
    /// Delegates handling to <see cref="UserService.HandleLoginAsync"/>.
    /// </summary>
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
                Console.WriteLine($"[ERROR] Failed to process login for '{request.Username}': {ex}");

                // Notify the client about the failure
                string senderTopic = TopicNames.CreatePrivateUserTopicName(request.Username);
                var errorEvent = new ErrorEvent(
                    $"Login failed: {ex.Message}"
                );
                await _bus.PubSub.PublishAsync(errorEvent, senderTopic);

                return new LoginResponse(false, "Login failed due to server error");
            }
        });
    }


    /// <summary>
    /// Subscribes to logout events via Pub/Sub.
    /// Delegates handling to <see cref="UserService.HandleLogoutAsync"/>.
    /// </summary>
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
                    Console.WriteLine($"[ERROR] Failed to process logout for '{request.Username}': {ex}");

                    // Notify the client about the failure
                    string senderTopic = TopicNames.CreatePrivateUserTopicName(request.Username);
                    var errorEvent = new ErrorEvent(
                        $"Logout failed: {ex.Message}"
                    );
                    await _bus.PubSub.PublishAsync(errorEvent, senderTopic);
                }
            }
        );
    }
}