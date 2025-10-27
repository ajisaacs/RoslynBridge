namespace RoslynBridge.WebApi.Models;

/// <summary>
/// Represents a single query history entry with request and response information
/// </summary>
public class QueryHistoryEntry
{
    /// <summary>
    /// Unique identifier for this history entry
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Timestamp when the request was received
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// The endpoint path that was called
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// HTTP method used
    /// </summary>
    public string Method { get; set; } = string.Empty;

    /// <summary>
    /// The Roslyn query request
    /// </summary>
    public RoslynQueryRequest? Request { get; set; }

    /// <summary>
    /// The Roslyn query response
    /// </summary>
    public RoslynQueryResponse? Response { get; set; }

    /// <summary>
    /// Request duration in milliseconds
    /// </summary>
    public long DurationMs { get; set; }

    /// <summary>
    /// Whether the request was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Client IP address
    /// </summary>
    public string? ClientIp { get; set; }
}
