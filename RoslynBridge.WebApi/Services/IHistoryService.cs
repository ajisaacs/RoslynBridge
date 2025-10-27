using RoslynBridge.WebApi.Models;

namespace RoslynBridge.WebApi.Services;

/// <summary>
/// Service for managing query history
/// </summary>
public interface IHistoryService
{
    /// <summary>
    /// Add a new history entry
    /// </summary>
    /// <param name="entry">The history entry to add</param>
    void Add(QueryHistoryEntry entry);

    /// <summary>
    /// Get all history entries
    /// </summary>
    /// <returns>List of all history entries</returns>
    IEnumerable<QueryHistoryEntry> GetAll();

    /// <summary>
    /// Get a specific history entry by ID
    /// </summary>
    /// <param name="id">The entry ID</param>
    /// <returns>The history entry, or null if not found</returns>
    QueryHistoryEntry? GetById(string id);

    /// <summary>
    /// Get recent history entries
    /// </summary>
    /// <param name="count">Number of entries to return</param>
    /// <returns>List of recent history entries</returns>
    IEnumerable<QueryHistoryEntry> GetRecent(int count = 50);

    /// <summary>
    /// Clear all history entries
    /// </summary>
    void Clear();

    /// <summary>
    /// Get total count of history entries
    /// </summary>
    /// <returns>Total number of entries</returns>
    int GetCount();
}
