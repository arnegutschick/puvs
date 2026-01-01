using System.Collections.Concurrent;

namespace ChatServer.Services;

public class StatisticsService
{
    private readonly ConcurrentDictionary<string, int> _counts = new();

    public void RegisterUser(string username)
    {
        _counts.TryAdd(username, 0);
    }

    public void RecordMessage(string username)
    {
        _counts.AddOrUpdate(username, 1, (_, c) => c + 1);
    }

    public (int total, double avg, IReadOnlyList<TopChatter> top3)
        Snapshot()
    {
        int total = _counts.Values.Sum();
        double avg = _counts.Count == 0
            ? 0
            : total / (double)_counts.Count;

        var top3 = _counts
            .OrderByDescending(kv => kv.Value)
            .Take(3)
            .Select(kv => new TopChatter(kv.Key, kv.Value))
            .ToList();

        return (total, avg, top3);
    }
}