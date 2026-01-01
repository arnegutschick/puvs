using Chat.Contracts;
using ChatServer.Infrastructure;
using ChatServer.Services;
using EasyNetQ;

namespace ChatServer.Handlers;

public class UserHandler
{
    private readonly IBus _bus;
    private readonly UserService _service;

    private const string LogoutSubscriptionId = TopicNames.LogoutSubscription;

    public UserHandler(IBus bus, UserService service)
    {
        _bus = bus;
        _service = service;
    }

    /// <summary>
    /// Registriert Login- und Logout-Handler.
    /// </summary>
    public void Start()
    {
        StartLogin();
        StartLogout();
    }

    private void StartLogin()
    {
        _bus.Rpc.RespondAsync<LoginRequest, LoginResponse>(request =>
        {
            return _service.HandleLoginAsync(request, _bus);
        });
    }

    private void StartLogout()
    {
        _bus.PubSub.SubscribeAsync<LogoutRequest>(
            LogoutSubscriptionId,
            async request => await _service.HandleLogoutAsync(request, _bus)
        );
    }
}
