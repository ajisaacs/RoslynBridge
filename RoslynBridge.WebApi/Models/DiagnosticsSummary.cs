namespace RoslynBridge.WebApi.Models;

/// <summary>
/// Summary of diagnostics counts by severity level
/// </summary>
public class DiagnosticsSummary
{
    /// <summary>
    /// Number of error diagnostics
    /// </summary>
    public int Errors { get; set; }

    /// <summary>
    /// Number of warning diagnostics
    /// </summary>
    public int Warnings { get; set; }

    /// <summary>
    /// Number of info diagnostics
    /// </summary>
    public int Info { get; set; }

    /// <summary>
    /// Number of hidden diagnostics
    /// </summary>
    public int Hidden { get; set; }

    /// <summary>
    /// Total number of diagnostics
    /// </summary>
    public int Total => Errors + Warnings + Info + Hidden;

    /// <summary>
    /// Optional: file path if summary is for a specific file
    /// </summary>
    public string? FilePath { get; set; }
}
