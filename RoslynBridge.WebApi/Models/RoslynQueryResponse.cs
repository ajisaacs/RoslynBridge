namespace RoslynBridge.WebApi.Models;

/// <summary>
/// Response model for Roslyn query operations
/// </summary>
public class RoslynQueryResponse
{
    /// <summary>
    /// Indicates whether the operation was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Optional message providing additional context
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// The response data (structure varies by query type)
    /// </summary>
    public object? Data { get; set; }

    /// <summary>
    /// Error message if the operation failed
    /// </summary>
    public string? Error { get; set; }
}
