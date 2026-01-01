using EasyNetQ;

namespace Chat.Contracts.Infrastructure;

public static class RabbitMqBusFactory
{
    /// <summary>
    /// Creates and configures an EasyNetQ bus instance.
    /// </summary>
    public static IBus Create(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException(
                "RabbitMQ connection string must not be empty.",
                nameof(connectionString));

        return RabbitHutch.CreateBus(connectionString);
    }
}
