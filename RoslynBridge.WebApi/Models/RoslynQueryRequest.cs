using System.ComponentModel.DataAnnotations;

namespace RoslynBridge.WebApi.Models;

/// <summary>
/// Request model for Roslyn query operations
/// </summary>
public class RoslynQueryRequest
{
    /// <summary>
    /// Type of query to execute (e.g., "getsymbol", "getdocument", "findreferences")
    /// </summary>
    [Required]
    public string QueryType { get; set; } = string.Empty;

    /// <summary>
    /// File path for file-based operations
    /// </summary>
    public string? FilePath { get; set; }

    /// <summary>
    /// Symbol name for symbol-based operations
    /// </summary>
    public string? SymbolName { get; set; }

    /// <summary>
    /// Line number (1-based) for position-based operations
    /// </summary>
    public int? Line { get; set; }

    /// <summary>
    /// Column number (0-based) for position-based operations
    /// </summary>
    public int? Column { get; set; }

    /// <summary>
    /// Additional parameters for the query
    /// </summary>
    public Dictionary<string, string>? Parameters { get; set; }

    /// <summary>
    /// Project name for project operations
    /// </summary>
    public string? ProjectName { get; set; }

    /// <summary>
    /// Package name for NuGet operations
    /// </summary>
    public string? PackageName { get; set; }

    /// <summary>
    /// Version for NuGet package operations
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    /// Build configuration (Debug/Release)
    /// </summary>
    public string? Configuration { get; set; }

    /// <summary>
    /// Directory path for directory operations
    /// </summary>
    public string? DirectoryPath { get; set; }
}
