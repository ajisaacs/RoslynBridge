using Microsoft.AspNetCore.Mvc;
using RoslynBridge.WebApi.Models;
using RoslynBridge.WebApi.Services;

namespace RoslynBridge.WebApi.Controllers;

/// <summary>
/// Health check and status controller
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class HealthController : ControllerBase
{
    private readonly IRoslynBridgeClient _bridgeClient;
    private readonly ILogger<HealthController> _logger;

    public HealthController(IRoslynBridgeClient bridgeClient, ILogger<HealthController> logger)
    {
        _bridgeClient = bridgeClient;
        _logger = logger;
    }

    /// <summary>
    /// Get the health status of the middleware and Visual Studio plugin
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Health status information</returns>
    /// <response code="200">Service is healthy</response>
    /// <response code="503">Service is unhealthy</response>
    [HttpGet]
    [ProducesResponseType(typeof(HealthCheckResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HealthCheckResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<HealthCheckResponse>> GetHealth(CancellationToken cancellationToken)
    {
        var response = new HealthCheckResponse();

        try
        {
            var isVsPluginHealthy = await _bridgeClient.IsHealthyAsync(null, cancellationToken);
            response.VsPluginStatus = isVsPluginHealthy ? "Connected" : "Disconnected";

            if (!isVsPluginHealthy)
            {
                response.Status = "Degraded";
                _logger.LogWarning("Visual Studio plugin is not accessible");
                return StatusCode(StatusCodes.Status503ServiceUnavailable, response);
            }

            _logger.LogInformation("Health check passed");
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed");
            response.Status = "Unhealthy";
            response.VsPluginStatus = "Error";
            return StatusCode(StatusCodes.Status503ServiceUnavailable, response);
        }
    }

    /// <summary>
    /// Simple ping endpoint
    /// </summary>
    /// <returns>Pong response</returns>
    [HttpGet("ping")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public IActionResult Ping()
    {
        return Ok(new { message = "pong", timestamp = DateTime.UtcNow });
    }
}
