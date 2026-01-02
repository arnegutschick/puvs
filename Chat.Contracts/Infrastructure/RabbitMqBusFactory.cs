using EasyNetQ;

namespace Chat.Contracts.Infrastructure;

/// <summary>
/// Factory class for creating and configuring RabbitMQ bus instances using EasyNetQ.
/// </summary>
public static class RabbitMqBusFactory
{
    /// <summary>
    /// Creates and configures a new <see cref="IBus"/> instance connected to the specified RabbitMQ broker.
    /// </summary>
    /// <param name="connectionString">
    /// The connection string used to connect to the RabbitMQ broker.
    /// </param>
    /// <returns>
    /// A configured instance of <see cref="IBus"/> that can be used to publish and subscribe to messages.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the provided <paramref name="connectionString"/> is null, empty, or consists only of whitespace.
    /// </exception>
    public static IBus Create(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException(
                "RabbitMQ connection string must not be empty.",
                nameof(connectionString));

        return RabbitHutch.CreateBus(connectionString);
    }
}
