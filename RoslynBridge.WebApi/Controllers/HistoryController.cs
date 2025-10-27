using Microsoft.AspNetCore.Mvc;
using RoslynBridge.WebApi.Models;
using RoslynBridge.WebApi.Services;

namespace RoslynBridge.WebApi.Controllers;

/// <summary>
/// Controller for accessing query history
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class HistoryController : ControllerBase
{
    private readonly IHistoryService _historyService;
    private readonly ILogger<HistoryController> _logger;

    public HistoryController(IHistoryService historyService, ILogger<HistoryController> logger)
    {
        _historyService = historyService;
        _logger = logger;
    }

    /// <summary>
    /// Get all history entries
    /// </summary>
    /// <returns>List of all history entries</returns>
    /// <response code="200">Returns the list of history entries</response>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<QueryHistoryEntry>), StatusCodes.Status200OK)]
    public ActionResult<IEnumerable<QueryHistoryEntry>> GetAll()
    {
        var entries = _historyService.GetAll();
        return Ok(entries);
    }

    /// <summary>
    /// Get a specific history entry by ID
    /// </summary>
    /// <param name="id">The history entry ID</param>
    /// <returns>The history entry</returns>
    /// <response code="200">Returns the history entry</response>
    /// <response code="404">Entry not found</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(QueryHistoryEntry), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<QueryHistoryEntry> GetById(string id)
    {
        var entry = _historyService.GetById(id);
        if (entry == null)
        {
            return NotFound(new { message = $"History entry {id} not found" });
        }

        return Ok(entry);
    }

    /// <summary>
    /// Get recent history entries
    /// </summary>
    /// <param name="count">Number of entries to return (default: 50, max: 500)</param>
    /// <returns>List of recent history entries</returns>
    /// <response code="200">Returns the list of recent entries</response>
    [HttpGet("recent")]
    [ProducesResponseType(typeof(IEnumerable<QueryHistoryEntry>), StatusCodes.Status200OK)]
    public ActionResult<IEnumerable<QueryHistoryEntry>> GetRecent([FromQuery] int count = 50)
    {
        if (count > 500) count = 500;
        if (count < 1) count = 1;

        var entries = _historyService.GetRecent(count);
        return Ok(entries);
    }

    /// <summary>
    /// Get history statistics
    /// </summary>
    /// <returns>Statistics about history entries</returns>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public ActionResult GetStats()
    {
        var entries = _historyService.GetAll();
        var stats = new
        {
            totalEntries = _historyService.GetCount(),
            successfulRequests = entries.Count(e => e.Success),
            failedRequests = entries.Count(e => !e.Success),
            averageDurationMs = entries.Any() ? entries.Average(e => e.DurationMs) : 0,
            oldestEntry = entries.Any() ? entries.Last().Timestamp : (DateTime?)null,
            newestEntry = entries.Any() ? entries.First().Timestamp : (DateTime?)null,
            topPaths = entries
                .GroupBy(e => e.Path)
                .OrderByDescending(g => g.Count())
                .Take(10)
                .Select(g => new { path = g.Key, count = g.Count() })
        };

        return Ok(stats);
    }

    /// <summary>
    /// Clear all history entries
    /// </summary>
    /// <returns>Confirmation message</returns>
    /// <response code="200">History cleared successfully</response>
    [HttpDelete]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public ActionResult Clear()
    {
        var count = _historyService.GetCount();
        _historyService.Clear();
        _logger.LogInformation("History cleared: {Count} entries removed", count);

        return Ok(new { message = $"History cleared: {count} entries removed" });
    }
}
