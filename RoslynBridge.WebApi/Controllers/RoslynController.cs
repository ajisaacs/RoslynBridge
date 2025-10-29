using Microsoft.AspNetCore.Mvc;
using RoslynBridge.WebApi.Models;
using RoslynBridge.WebApi.Services;
using RoslynBridge.WebApi.Utilities;

namespace RoslynBridge.WebApi.Controllers;

/// <summary>
/// Controller for Roslyn code analysis operations
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class RoslynController : ControllerBase
{
    private readonly IRoslynBridgeClient _bridgeClient;
    private readonly ILogger<RoslynController> _logger;

    public RoslynController(IRoslynBridgeClient bridgeClient, ILogger<RoslynController> logger)
    {
        _bridgeClient = bridgeClient;
        _logger = logger;
    }

    /// <summary>
    /// Execute a Roslyn query
    /// </summary>
    /// <param name="request">The query request</param>
    /// <param name="instancePort">Optional: specific VS instance port to target</param>
    /// <param name="solutionName">Optional: solution name to route to (e.g., "RoslynBridge")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The query result</returns>
    /// <response code="200">Query executed successfully</response>
    /// <response code="400">Invalid request</response>
    /// <response code="500">Internal server error</response>
    [HttpPost("query")]
    [ProducesResponseType(typeof(RoslynQueryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<RoslynQueryResponse>> ExecuteQuery(
        [FromBody] RoslynQueryRequest request,
        [FromQuery] int? instancePort = null,
        [FromQuery] string? solutionName = null,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        _logger.LogInformation("Received query request: {QueryType}", request.QueryType);

        var result = await _bridgeClient.ExecuteQueryAsync(request, instancePort, solutionName, cancellationToken);

        if (!result.Success)
        {
            _logger.LogWarning("Query failed: {Error}", result.Error);
        }

        return Ok(result);
    }

    /// <summary>
    /// Get all projects in the solution
    /// </summary>
    /// <param name="instancePort">Optional: specific VS instance port to target</param>
    /// <param name="solutionName">Optional: solution name to route to (e.g., "RoslynBridge")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of projects</returns>
    [HttpGet("projects")]
    [ProducesResponseType(typeof(RoslynQueryResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<RoslynQueryResponse>> GetProjects(
        [FromQuery] int? instancePort = null,
        [FromQuery] string? solutionName = null,
        CancellationToken cancellationToken = default)
    {
        var request = new RoslynQueryRequest { QueryType = "getprojects" };
        var result = await _bridgeClient.ExecuteQueryAsync(request, instancePort, solutionName, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Get solution overview
    /// </summary>
    /// <param name="instancePort">Optional: specific VS instance port to target</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Solution statistics and overview</returns>
    [HttpGet("solution/overview")]
    [ProducesResponseType(typeof(RoslynQueryResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<RoslynQueryResponse>> GetSolutionOverview(
        [FromQuery] int? instancePort = null,
        CancellationToken cancellationToken = default)
    {
        var request = new RoslynQueryRequest { QueryType = "getsolutionoverview" };
        var result = await _bridgeClient.ExecuteQueryAsync(request, instancePort, null, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Get diagnostics (errors and warnings)
    /// </summary>
    /// <param name="filePath">Optional file path to filter diagnostics</param>
    /// <param name="severity">Optional severity filter (error, warning, info, hidden) - comma separated for multiple</param>
    /// <param name="instancePort">Optional: specific VS instance port to target</param>
    /// <param name="solutionName">Optional: solution name to route to (e.g., "RoslynBridge")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of diagnostics</returns>
    [HttpGet("diagnostics")]
    [ProducesResponseType(typeof(RoslynQueryResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<RoslynQueryResponse>> GetDiagnostics(
        [FromQuery] string? filePath = null,
        [FromQuery] string? severity = null,
        [FromQuery] int? instancePort = null,
        [FromQuery] string? solutionName = null,
        CancellationToken cancellationToken = default)
    {
        var request = new RoslynQueryRequest
        {
            QueryType = "getdiagnostics",
            FilePath = filePath
        };

        // Add severity filter to parameters if provided
        if (!string.IsNullOrEmpty(severity))
        {
            request.Parameters = new Dictionary<string, string> { ["severity"] = severity };
        }

        var result = await _bridgeClient.ExecuteQueryAsync(request, instancePort, solutionName, cancellationToken);

        // If severity filter was requested, apply it to the response data
        if (!string.IsNullOrEmpty(severity) && result.Success && result.Data != null)
        {
            result = FilterDiagnosticsBySeverity(result, severity);
        }

        return Ok(result);
    }

    /// <summary>
    /// Get diagnostics summary (counts by severity)
    /// </summary>
    /// <param name="filePath">Optional file path to filter diagnostics</param>
    /// <param name="instancePort">Optional: specific VS instance port to target</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Diagnostics summary with counts by severity</returns>
    [HttpGet("diagnostics/summary")]
    [ProducesResponseType(typeof(RoslynQueryResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<RoslynQueryResponse>> GetDiagnosticsSummary(
        [FromQuery] string? filePath = null,
        [FromQuery] int? instancePort = null,
        CancellationToken cancellationToken = default)
    {
        var request = new RoslynQueryRequest
        {
            QueryType = "getdiagnostics",
            FilePath = filePath
        };
        var result = await _bridgeClient.ExecuteQueryAsync(request, instancePort, null, cancellationToken);

        if (!result.Success || result.Data == null)
        {
            return Ok(result);
        }

        // Convert full diagnostics to summary
        var summary = CreateDiagnosticsSummary(result.Data, filePath);

        return Ok(new RoslynQueryResponse
        {
            Success = true,
            Data = summary,
            Message = $"Summary generated for {summary.Total} diagnostics"
        });
    }

    /// <summary>
    /// Get diagnostics count
    /// </summary>
    /// <param name="filePath">Optional file path to filter diagnostics</param>
    /// <param name="severity">Optional severity filter (error, warning, info, hidden)</param>
    /// <param name="instancePort">Optional: specific VS instance port to target</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Count of diagnostics matching the criteria</returns>
    [HttpGet("diagnostics/count")]
    [ProducesResponseType(typeof(RoslynQueryResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<RoslynQueryResponse>> GetDiagnosticsCount(
        [FromQuery] string? filePath = null,
        [FromQuery] string? severity = null,
        [FromQuery] int? instancePort = null,
        CancellationToken cancellationToken = default)
    {
        var request = new RoslynQueryRequest
        {
            QueryType = "getdiagnostics",
            FilePath = filePath
        };
        var result = await _bridgeClient.ExecuteQueryAsync(request, instancePort, null, cancellationToken);

        if (!result.Success || result.Data == null)
        {
            return Ok(new RoslynQueryResponse
            {
                Success = result.Success,
                Data = new { count = 0 },
                Error = result.Error
            });
        }

        int count = CountDiagnostics(result.Data, severity);

        return Ok(new RoslynQueryResponse
        {
            Success = true,
            Data = new { count },
            Message = $"Found {count} diagnostic(s)"
        });
    }

    private DiagnosticsSummary CreateDiagnosticsSummary(object data, string? filePath)
    {
        var summary = new DiagnosticsSummary { FilePath = filePath };

        if (data is System.Text.Json.JsonElement jsonElement && jsonElement.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            foreach (var diagnostic in jsonElement.EnumerateArray())
            {
                if (diagnostic.TryGetProperty("severity", out var severityProp))
                {
                    var severityValue = severityProp.GetString()?.ToLowerInvariant();
                    switch (severityValue)
                    {
                        case "error":
                            summary.Errors++;
                            break;
                        case "warning":
                            summary.Warnings++;
                            break;
                        case "info":
                            summary.Info++;
                            break;
                        case "hidden":
                            summary.Hidden++;
                            break;
                    }
                }
            }
        }

        return summary;
    }

    private int CountDiagnostics(object data, string? severityFilter)
    {
        int count = 0;
        var severitySet = ParseSeverityFilter(severityFilter);

        if (data is System.Text.Json.JsonElement jsonElement && jsonElement.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            foreach (var diagnostic in jsonElement.EnumerateArray())
            {
                if (severitySet.Count == 0)
                {
                    count++;
                }
                else if (diagnostic.TryGetProperty("severity", out var severityProp))
                {
                    var severityValue = severityProp.GetString()?.ToLowerInvariant();
                    if (severityValue != null && severitySet.Contains(severityValue))
                    {
                        count++;
                    }
                }
            }
        }

        return count;
    }

    private RoslynQueryResponse FilterDiagnosticsBySeverity(RoslynQueryResponse response, string severityFilter)
    {
        var severitySet = ParseSeverityFilter(severityFilter);

        if (response.Data is System.Text.Json.JsonElement jsonElement && jsonElement.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            var filtered = new List<System.Text.Json.JsonElement>();

            foreach (var diagnostic in jsonElement.EnumerateArray())
            {
                if (diagnostic.TryGetProperty("severity", out var severityProp))
                {
                    var severityValue = severityProp.GetString()?.ToLowerInvariant();
                    if (severityValue != null && severitySet.Contains(severityValue))
                    {
                        filtered.Add(diagnostic);
                    }
                }
            }

            return new RoslynQueryResponse
            {
                Success = true,
                Data = filtered,
                Message = $"Filtered to {filtered.Count} diagnostic(s)"
            };
        }

        return response;
    }

    private HashSet<string> ParseSeverityFilter(string? severityFilter)
    {
        var severitySet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrEmpty(severityFilter))
        {
            var severities = severityFilter.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var sev in severities)
            {
                severitySet.Add(sev.ToLowerInvariant());
            }
        }

        return severitySet;
    }

    /// <summary>
    /// Get symbol information at a specific position
    /// </summary>
    /// <param name="filePath">File path</param>
    /// <param name="line">Line number (1-based)</param>
    /// <param name="column">Column number (0-based)</param>
    /// <param name="fields">Optional: comma-separated list of fields to include in response (e.g., "name,kind,location")</param>
    /// <param name="instancePort">Optional: specific VS instance port to target</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Symbol information</returns>
    [HttpGet("symbol")]
    [ProducesResponseType(typeof(RoslynQueryResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<RoslynQueryResponse>> GetSymbol(
        [FromQuery] string filePath,
        [FromQuery] int line,
        [FromQuery] int column,
        [FromQuery] string? fields = null,
        [FromQuery] int? instancePort = null,
        CancellationToken cancellationToken = default)
    {
        var request = new RoslynQueryRequest
        {
            QueryType = "getsymbol",
            FilePath = filePath,
            Line = line,
            Column = column
        };
        var result = await _bridgeClient.ExecuteQueryAsync(request, instancePort, null, cancellationToken);

        // Apply field projection if requested
        if (!string.IsNullOrEmpty(fields) && result.Success && result.Data != null)
        {
            result.Data = ResponseProjection.ProjectFields(result.Data, fields);
        }

        return Ok(result);
    }

    /// <summary>
    /// Find all references to a symbol
    /// </summary>
    /// <param name="filePath">File path</param>
    /// <param name="line">Line number (1-based)</param>
    /// <param name="column">Column number (0-based)</param>
    /// <param name="fields">Optional: comma-separated list of fields to include in response (e.g., "filePath,line,column")</param>
    /// <param name="instancePort">Optional: specific VS instance port to target</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of references</returns>
    [HttpGet("references")]
    [ProducesResponseType(typeof(RoslynQueryResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<RoslynQueryResponse>> FindReferences(
        [FromQuery] string filePath,
        [FromQuery] int line,
        [FromQuery] int column,
        [FromQuery] string? fields = null,
        [FromQuery] int? instancePort = null,
        CancellationToken cancellationToken = default)
    {
        var request = new RoslynQueryRequest
        {
            QueryType = "findreferences",
            FilePath = filePath,
            Line = line,
            Column = column
        };
        var result = await _bridgeClient.ExecuteQueryAsync(request, instancePort, null, cancellationToken);

        // Apply field projection if requested
        if (!string.IsNullOrEmpty(fields) && result.Success && result.Data != null)
        {
            result.Data = ResponseProjection.ProjectFields(result.Data, fields);
        }

        return Ok(result);
    }

    /// <summary>
    /// Get count of references to a symbol
    /// </summary>
    /// <param name="filePath">File path</param>
    /// <param name="line">Line number (1-based)</param>
    /// <param name="column">Column number (0-based)</param>
    /// <param name="instancePort">Optional: specific VS instance port to target</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Count of references</returns>
    [HttpGet("references/count")]
    [ProducesResponseType(typeof(RoslynQueryResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<RoslynQueryResponse>> GetReferencesCount(
        [FromQuery] string filePath,
        [FromQuery] int line,
        [FromQuery] int column,
        [FromQuery] int? instancePort = null,
        CancellationToken cancellationToken = default)
    {
        var request = new RoslynQueryRequest
        {
            QueryType = "findreferences",
            FilePath = filePath,
            Line = line,
            Column = column
        };
        var result = await _bridgeClient.ExecuteQueryAsync(request, instancePort, null, cancellationToken);

        if (!result.Success || result.Data == null)
        {
            return Ok(new RoslynQueryResponse
            {
                Success = result.Success,
                Data = new { count = 0 },
                Error = result.Error
            });
        }

        int count = CountItems(result.Data);

        return Ok(new RoslynQueryResponse
        {
            Success = true,
            Data = new { count },
            Message = $"Found {count} reference(s)"
        });
    }

    private int CountItems(object data)
    {
        if (data is System.Text.Json.JsonElement jsonElement && jsonElement.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            return jsonElement.GetArrayLength();
        }

        if (data is System.Collections.ICollection collection)
        {
            return collection.Count;
        }

        if (data is System.Collections.IEnumerable enumerable and not string)
        {
            int count = 0;
            foreach (var _ in enumerable)
            {
                count++;
            }
            return count;
        }

        return 0;
    }

    /// <summary>
    /// Search for symbols by name
    /// </summary>
    /// <param name="symbolName">Symbol name or pattern</param>
    /// <param name="kind">Optional symbol kind filter</param>
    /// <param name="instancePort">Optional: specific VS instance port to target</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of matching symbols</returns>
    [HttpGet("symbol/search")]
    [ProducesResponseType(typeof(RoslynQueryResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<RoslynQueryResponse>> FindSymbol(
        [FromQuery] string symbolName,
        [FromQuery] string? kind = null,
        [FromQuery] int? instancePort = null,
        CancellationToken cancellationToken = default)
    {
        var request = new RoslynQueryRequest
        {
            QueryType = "findsymbol",
            SymbolName = symbolName
        };

        if (!string.IsNullOrEmpty(kind))
        {
            request.Parameters = new Dictionary<string, string> { ["kind"] = kind };
        }

        var result = await _bridgeClient.ExecuteQueryAsync(request, instancePort, null, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Format a document
    /// </summary>
    /// <param name="filePath">File path to format</param>
    /// <param name="instancePort">Optional: specific VS instance port to target</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Format operation result</returns>
    [HttpPost("format")]
    [ProducesResponseType(typeof(RoslynQueryResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<RoslynQueryResponse>> FormatDocument(
        [FromBody] string filePath,
        [FromQuery] int? instancePort = null,
        CancellationToken cancellationToken = default)
    {
        var request = new RoslynQueryRequest
        {
            QueryType = "formatdocument",
            FilePath = filePath
        };
        var result = await _bridgeClient.ExecuteQueryAsync(request, instancePort, null, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Add a NuGet package to a project
    /// </summary>
    /// <param name="projectName">Project name</param>
    /// <param name="packageName">NuGet package name</param>
    /// <param name="version">Optional package version</param>
    /// <param name="instancePort">Optional: specific VS instance port to target</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Operation result</returns>
    [HttpPost("project/package/add")]
    [ProducesResponseType(typeof(RoslynQueryResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<RoslynQueryResponse>> AddNuGetPackage(
        [FromQuery] string projectName,
        [FromQuery] string packageName,
        [FromQuery] string? version = null,
        [FromQuery] int? instancePort = null,
        CancellationToken cancellationToken = default)
    {
        var request = new RoslynQueryRequest
        {
            QueryType = "addnugetpackage",
            ProjectName = projectName,
            PackageName = packageName,
            Version = version
        };
        var result = await _bridgeClient.ExecuteQueryAsync(request, instancePort, null, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Build a project
    /// </summary>
    /// <param name="projectName">Project name</param>
    /// <param name="configuration">Build configuration (Debug/Release)</param>
    /// <param name="instancePort">Optional: specific VS instance port to target</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Build result</returns>
    [HttpPost("project/build")]
    [ProducesResponseType(typeof(RoslynQueryResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<RoslynQueryResponse>> BuildProject(
        [FromQuery] string projectName,
        [FromQuery] string? configuration = null,
        [FromQuery] int? instancePort = null,
        CancellationToken cancellationToken = default)
    {
        var request = new RoslynQueryRequest
        {
            QueryType = "buildproject",
            ProjectName = projectName,
            Configuration = configuration
        };
        var result = await _bridgeClient.ExecuteQueryAsync(request, instancePort, null, cancellationToken);
        return Ok(result);
    }
}
