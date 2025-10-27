using System.Collections.Concurrent;
using RoslynBridge.WebApi.Models;

namespace RoslynBridge.WebApi.Services;

/// <summary>
/// In-memory implementation of history service
/// </summary>
public class HistoryService : IHistoryService
{
    private readonly ConcurrentQueue<QueryHistoryEntry> _entries = new();
    private readonly ILogger<HistoryService> _logger;
    private readonly int _maxEntries;

    public HistoryService(ILogger<HistoryService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _maxEntries = configuration.GetValue<int>("History:MaxEntries", 1000);
    }

    public void Add(QueryHistoryEntry entry)
    {
        _entries.Enqueue(entry);

        // Trim old entries if we exceed max
        while (_entries.Count > _maxEntries)
        {
            _entries.TryDequeue(out _);
        }

        _logger.LogDebug("Added history entry: {Id} - {Path}", entry.Id, entry.Path);
    }

    public IEnumerable<QueryHistoryEntry> GetAll()
    {
        return _entries.Reverse();
    }

    public QueryHistoryEntry? GetById(string id)
    {
        return _entries.FirstOrDefault(e => e.Id == id);
    }

    public IEnumerable<QueryHistoryEntry> GetRecent(int count = 50)
    {
        return _entries.Reverse().Take(count);
    }

    public void Clear()
    {
        _entries.Clear();
        _logger.LogInformation("History cleared");
    }

    public int GetCount()
    {
        return _entries.Count;
    }
}
