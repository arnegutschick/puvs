using System.Collections.Concurrent;

namespace ChatServer;

public class StatisticsStore
{
    private readonly ConcurrentDictionary<string, byte> _knownUsers = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, long> _messageCounts = new(StringComparer.OrdinalIgnoreCase);
    private long _totalMessages = 0;

    /// <summary>
    /// Registers a user for the Statistics.
    /// <param name="user">Name of the user to register.</param>
    /// </summary>
    public void RegisterUser(string user)
    {
        if (!string.IsNullOrWhiteSpace(user))
            _knownUsers.TryAdd(user.Trim(), 0);
    }

    /// <summary>
    /// Records amount of messages sent by a user.
    /// <param name="user">Name of the user that sent the message.</param>
    /// </summary>
    public void RecordMessage(string user)
    {
        if (string.IsNullOrWhiteSpace(user)) return;

        user = user.Trim();
        _knownUsers.TryAdd(user, 0);

        _messageCounts.AddOrUpdate(user, 1, (_, current) => current + 1);
        Interlocked.Increment(ref _totalMessages);
    }

    /// <summary>
    /// Creates a consistent statistics snapshot of the current chat usage.
    /// Computes the total number of messages, the average number of messages per known user
    /// and a Top 3 list of the most active users by message count.
    /// <param name="Total">Total number of recorded chat messages</param>
    /// <param name="Avg">Average messages per known user (0 if no users exist)</param>
    /// <param name="Total">Array of up to 3 users with the highest message counts.</param>
    /// </summary>
    public (long Total, double Avg, (string User, long Count)[] Top3) BuildSnapshot()
    {
        var total = Interlocked.Read(ref _totalMessages);
        var userCount = _knownUsers.Count;
        var avg = userCount == 0 ? 0 : (double)total / userCount;

        var top3 = _messageCounts
            .ToArray()
            .OrderByDescending(kv => kv.Value)
            .Take(3)
            .Select(kv => (kv.Key, kv.Value))
            .ToArray();

        return (total, avg, top3);
    }
}
