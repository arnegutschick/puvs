using Microsoft.Extensions.Configuration;
using EasyNetQ;
using Chat.Contracts;
using ChatClient.Infrastructure;

namespace ChatClient.Services
{
    /// <summary>
    /// Manages periodic heartbeat messages to keep the client's presence known on the server.
    /// Uses a timer to publish client heartbeat messages at configurable intervals.
    /// </summary>
    public class HeartbeatService
    {
        private readonly IConfiguration _configuration;
        private readonly BusPublisher _busPublisher;
        private readonly IBus _bus;
        private readonly string _username;
        private Timer? _timer;

        /// <summary>
        /// Initializes a new instance of the <see cref="HeartbeatService"/> class.
        /// </summary>
        /// <param name="bus">The Pub/Sub bus for sending heartbeat messages.</param>
        /// <param name="configuration">Configuration containing the heartbeat interval setting.</param>
        /// <param name="busPublisher">Helper for error-safe publish operations.</param>
        /// <param name="username">The current user's username.</param>
        public HeartbeatService(IBus bus, IConfiguration configuration, BusPublisher busPublisher, string username)
        {
            _bus = bus;
            _configuration = configuration;
            _busPublisher = busPublisher;
            _username = username;
        }

        /// <summary>
        /// Starts sending periodic client heartbeat messages at the configured interval.
        /// </summary>
        public async Task Start()
        {
            int intervalSeconds = _configuration.GetValue("ChatSettings:ClientHeartbeatIntervalSeconds", 10);

            _timer = new Timer(async _ =>
            {
                await _busPublisher.TryPublishAsync(() => _bus.PubSub.PublishAsync(new ClientHeatbeat(_username)));
            }, null, TimeSpan.Zero, TimeSpan.FromSeconds(intervalSeconds));
        }

        /// <summary>
        /// Stops sending heartbeat messages and disposes the timer.
        /// </summary>
        public void Stop()
        {
            _timer?.Dispose();
        }
    }
}
