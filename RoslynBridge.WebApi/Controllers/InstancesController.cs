using Microsoft.AspNetCore.Mvc;
using RoslynBridge.WebApi.Models;
using RoslynBridge.WebApi.Services;

namespace RoslynBridge.WebApi.Controllers;

/// <summary>
/// Controller for managing Visual Studio instance registrations
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class InstancesController : ControllerBase
{
    private readonly IInstanceRegistryService _registryService;
    private readonly ILogger<InstancesController> _logger;

    public InstancesController(
        IInstanceRegistryService registryService,
        ILogger<InstancesController> logger)
    {
        _registryService = registryService;
        _logger = logger;
    }

    /// <summary>
    /// Register a new Visual Studio instance
    /// </summary>
    /// <param name="instance">Instance information</param>
    /// <returns>Registration result</returns>
    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult Register([FromBody] VSInstanceInfo instance)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        _registryService.Register(instance);

        return Ok(new
        {
            success = true,
            message = "Instance registered successfully",
            processId = instance.ProcessId,
            port = instance.Port
        });
    }

    /// <summary>
    /// Unregister a Visual Studio instance
    /// </summary>
    /// <param name="processId">Process ID of the instance to unregister</param>
    /// <returns>Unregistration result</returns>
    [HttpPost("unregister/{processId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Unregister(int processId)
    {
        var removed = _registryService.Unregister(processId);

        if (!removed)
        {
            return NotFound(new { success = false, message = "Instance not found" });
        }

        return Ok(new { success = true, message = "Instance unregistered successfully" });
    }

    /// <summary>
    /// Update heartbeat for a Visual Studio instance (with full instance info update)
    /// </summary>
    /// <param name="instance">Updated instance information</param>
    /// <returns>Heartbeat update result</returns>
    [HttpPost("heartbeat")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult Heartbeat([FromBody] VSInstanceInfo instance)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        // Register/update the instance with current info
        _registryService.Register(instance);

        return Ok(new { success = true, message = "Heartbeat updated" });
    }

    /// <summary>
    /// Update heartbeat for a Visual Studio instance (legacy endpoint)
    /// </summary>
    /// <param name="processId">Process ID of the instance</param>
    /// <returns>Heartbeat update result</returns>
    [HttpPost("heartbeat/{processId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult HeartbeatById(int processId)
    {
        var updated = _registryService.UpdateHeartbeat(processId);

        if (!updated)
        {
            return NotFound(new { success = false, message = "Instance not found" });
        }

        return Ok(new { success = true, message = "Heartbeat updated" });
    }

    /// <summary>
    /// Get all registered Visual Studio instances
    /// </summary>
    /// <returns>List of registered instances</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<VSInstanceInfo>), StatusCodes.Status200OK)]
    public IActionResult GetAll()
    {
        var instances = _registryService.GetAllInstances();
        return Ok(instances);
    }

    /// <summary>
    /// Get instance by process ID
    /// </summary>
    /// <param name="processId">Process ID</param>
    /// <returns>Instance information</returns>
    [HttpGet("by-pid/{processId}")]
    [ProducesResponseType(typeof(VSInstanceInfo), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetByProcessId(int processId)
    {
        var instance = _registryService.GetByProcessId(processId);

        if (instance == null)
        {
            return NotFound(new { success = false, message = "Instance not found" });
        }

        return Ok(instance);
    }

    /// <summary>
    /// Get instance by solution path
    /// </summary>
    /// <param name="solutionPath">Solution file path</param>
    /// <returns>Instance information</returns>
    [HttpGet("by-solution")]
    [ProducesResponseType(typeof(VSInstanceInfo), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetBySolutionPath([FromQuery] string solutionPath)
    {
        if (string.IsNullOrEmpty(solutionPath))
        {
            return BadRequest(new { success = false, message = "Solution path is required" });
        }

        var instance = _registryService.GetBySolutionPath(solutionPath);

        if (instance == null)
        {
            return NotFound(new { success = false, message = "No instance found for this solution" });
        }

        return Ok(instance);
    }

    /// <summary>
    /// Get instance by port
    /// </summary>
    /// <param name="port">Port number</param>
    /// <returns>Instance information</returns>
    [HttpGet("by-port/{port}")]
    [ProducesResponseType(typeof(VSInstanceInfo), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetByPort(int port)
    {
        var instance = _registryService.GetByPort(port);

        if (instance == null)
        {
            return NotFound(new { success = false, message = "Instance not found" });
        }

        return Ok(instance);
    }
}
