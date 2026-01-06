using EasyNetQ;
using EasyNetQ.Topology;

namespace Chat.Contracts.Infrastructure;


/// <summary>
/// Factory class responsible for creating and validating RabbitMQ bus instances
/// using EasyNetQ.
/// 
/// The factory ensures that the returned <see cref="IBus"/> instance is fully
/// initialized and that a connection to the RabbitMQ broker can be successfully
/// established.
/// </summary>
public static class RabbitMqBusFactory
{
    /// <summary>
    /// Creates a new <see cref="IBus"/> instance and validates the connection
    /// to the RabbitMQ broker by performing a lightweight connectivity check.
    /// </summary>
    /// <param name="connectionString">
    /// The connection string used to connect to the RabbitMQ broker.
    /// </param>
    /// <returns>
    /// A fully initialized and validated <see cref="IBus"/> instance that can be
    /// safely used to publish and subscribe to messages.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the provided <paramref name="connectionString"/> is null,
    /// empty, or consists only of whitespace.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the connection to the RabbitMQ broker cannot be established
    /// or the validation check fails.
    /// </exception>
    public static async Task<IBus> CreateAndValidateAsync(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException(
                "RabbitMQ connection string must not be empty.",
                nameof(connectionString));

        var bus = RabbitHutch.CreateBus(connectionString);

        try
        {
            var advancedBus = bus.Advanced;

            // lightweight connectivity check
            var testExchange = await advancedBus.ExchangeDeclareAsync(
                "ping_check",
                ExchangeType.Fanout,
                autoDelete: true);

            await advancedBus.ExchangeDeleteAsync(testExchange);
        }
        catch
        {
            bus.Dispose();
            throw new InvalidOperationException(
                "Failed to connect to RabbitMQ using the provided connection string.");
        }

        return bus;
    }
}
