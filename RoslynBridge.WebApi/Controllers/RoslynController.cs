using Microsoft.AspNetCore.Mvc;
using RoslynBridge.WebApi.Models;
using RoslynBridge.WebApi.Services;

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
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        _logger.LogInformation("Received query request: {QueryType}", request.QueryType);

        var result = await _bridgeClient.ExecuteQueryAsync(request, instancePort, cancellationToken);

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
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of projects</returns>
    [HttpGet("projects")]
    [ProducesResponseType(typeof(RoslynQueryResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<RoslynQueryResponse>> GetProjects(
        [FromQuery] int? instancePort = null,
        CancellationToken cancellationToken = default)
    {
        var request = new RoslynQueryRequest { QueryType = "getprojects" };
        var result = await _bridgeClient.ExecuteQueryAsync(request, instancePort, cancellationToken);
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
        var result = await _bridgeClient.ExecuteQueryAsync(request, instancePort, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Get diagnostics (errors and warnings)
    /// </summary>
    /// <param name="filePath">Optional file path to filter diagnostics</param>
    /// <param name="instancePort">Optional: specific VS instance port to target</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of diagnostics</returns>
    [HttpGet("diagnostics")]
    [ProducesResponseType(typeof(RoslynQueryResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<RoslynQueryResponse>> GetDiagnostics(
        [FromQuery] string? filePath = null,
        [FromQuery] int? instancePort = null,
        CancellationToken cancellationToken = default)
    {
        var request = new RoslynQueryRequest
        {
            QueryType = "getdiagnostics",
            FilePath = filePath
        };
        var result = await _bridgeClient.ExecuteQueryAsync(request, instancePort, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Get symbol information at a specific position
    /// </summary>
    /// <param name="filePath">File path</param>
    /// <param name="line">Line number (1-based)</param>
    /// <param name="column">Column number (0-based)</param>
    /// <param name="instancePort">Optional: specific VS instance port to target</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Symbol information</returns>
    [HttpGet("symbol")]
    [ProducesResponseType(typeof(RoslynQueryResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<RoslynQueryResponse>> GetSymbol(
        [FromQuery] string filePath,
        [FromQuery] int line,
        [FromQuery] int column,
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
        var result = await _bridgeClient.ExecuteQueryAsync(request, instancePort, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Find all references to a symbol
    /// </summary>
    /// <param name="filePath">File path</param>
    /// <param name="line">Line number (1-based)</param>
    /// <param name="column">Column number (0-based)</param>
    /// <param name="instancePort">Optional: specific VS instance port to target</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of references</returns>
    [HttpGet("references")]
    [ProducesResponseType(typeof(RoslynQueryResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<RoslynQueryResponse>> FindReferences(
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
        var result = await _bridgeClient.ExecuteQueryAsync(request, instancePort, cancellationToken);
        return Ok(result);
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

        var result = await _bridgeClient.ExecuteQueryAsync(request, instancePort, cancellationToken);
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
        var result = await _bridgeClient.ExecuteQueryAsync(request, instancePort, cancellationToken);
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
        var result = await _bridgeClient.ExecuteQueryAsync(request, instancePort, cancellationToken);
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
        var result = await _bridgeClient.ExecuteQueryAsync(request, instancePort, cancellationToken);
        return Ok(result);
    }
}
