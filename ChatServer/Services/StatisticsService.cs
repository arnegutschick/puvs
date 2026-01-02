using System.Collections.Concurrent;

namespace ChatServer.Services;

/// <summary>
/// Service responsible for tracking chat message statistics per user.
/// Maintains total message counts, calculates averages, and identifies top chatters.
/// </summary>
public class StatisticsService
{
    // Thread-safe dictionary mapping usernames to their message counts
    private readonly ConcurrentDictionary<string, int> _userMessageCounts = new();

    /// <summary>
    /// Registers a new user in the statistics tracker.
    /// Initializes their message count to zero.
    /// </summary>
    /// <param name="username">The username to register.</param>
    public void RegisterUser(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            Console.WriteLine("[WARNING] Attempted to register null or empty username in StatisticsService.");
            return;
        }
        
        _userMessageCounts.TryAdd(username, 0);
    }


    /// <summary>
    /// Records that a user has sent a message.
    /// Increments their message count by 1.
    /// </summary>
    /// <param name="username">The username of the sender.</param>
    public void RecordMessage(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            Console.WriteLine("[WARNING] Attempted to record message for null or empty username in StatisticsService.");
            return;
        }

        _userMessageCounts.AddOrUpdate(username, 1, (_, c) => c + 1);
    }


    /// <summary>
    /// Returns a snapshot of the current statistics.
    /// </summary>
    /// <returns>
    /// A tuple containing:
    /// - total: Total number of messages sent by all users.
    /// - avg: Average messages per user.
    /// - top3: List of the top 3 most active chatters as <see cref="TopChatter"/> objects.
    /// </returns>
    public (int total, double avg, IReadOnlyList<TopChatter> top3) Snapshot()
    {
        int totalMessages = _userMessageCounts.Values.Sum(); // Total messages across all users
        double averageMessageCount = _userMessageCounts.Count == 0
            ? 0
            : totalMessages / (double)_userMessageCounts.Count; // Average per user

        // Identify top 3 users by message count
        var top3Users = _userMessageCounts
            .OrderByDescending(kv => kv.Value)
            .Take(3)
            .Select(kv => new TopChatter(kv.Key, kv.Value))
            .ToList();

        return (totalMessages, averageMessageCount, top3Users);
    }
}